using Financial.Services.Dashboard;
using Financial.Services.Notifications;
using Financial.Services.Reports;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Financial.Services.Receivables
{
    /// <summary>สถานะงานดึงลูกหนี้ (ทีละงาน) — ใช้แสดงความคืบหน้าบนหน้าเว็บ</summary>
    public sealed record ReceivableSyncStatus(string State, DateTime UpdatedAt, string? Message = null, DateTime? StartedAt = null, int RowsRead = 0,
        int DaysBack = 0, ReceivableSyncResult? Result = null)
    {
        public bool IsActive => State is RefreshStatus.QUEUED or RefreshStatus.RUNNING;
    }

    /// <summary>คิวงานดึงลูกหนี้จาก HIS (ใช้ร่วมทั้งแอป) — รับครั้งละ 1 งาน</summary>
    public class ReceivableSyncQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
        private ReceivableSyncStatus? _status;
        private readonly Lock _lock = new();

        public ChannelReader<int> Reader => _channel.Reader;

        public ReceivableSyncStatus? Status => _status;

        /// <returns>false ถ้ามีงานอยู่ในคิว/กำลังทำ</returns>
        public bool Enqueue(int daysBack)
        {
            lock (_lock)
            {
                if (_status?.IsActive == true) return false;
                _status = new ReceivableSyncStatus(RefreshStatus.QUEUED, DateTime.Now, "รอดำเนินการ", DaysBack: daysBack);
                return _channel.Writer.TryWrite(daysBack);
            }
        }

        public void SetStatus(ReceivableSyncStatus status) => _status = status;
    }

    public static class ReceivableSyncMessages
    {
        public static string Done(ReceivableSyncResult r, int daysBack, TimeSpan elapsed)
            => $"✅ ดึงลูกหนี้สิทธิจาก HIS ย้อนหลัง {daysBack} วัน สำเร็จ: บิล {r.SourceRows:N0}, ใหม่ {r.Added:N0}, เปลี่ยน {r.Updated:N0}, ยกเลิก {r.Cancelled:N0} ({(int)elapsed.TotalMinutes} นาที {elapsed.Seconds} วินาที)";

        public static string Failed(string exceptionType)
            => $"❌ ดึงลูกหนี้สิทธิจาก HIS ไม่สำเร็จ ({exceptionType}) — ดูรายละเอียดใน log ของเซิร์ฟเวอร์";
    }

    /// <summary>
    /// งานเบื้องหลัง: ทำตามคิว (ปุ่มดึงข้อมูล) และดึงอัตโนมัติทุกคืน (Receivables:AutoSyncEnabled / AutoSyncHour / SyncDaysBack)
    /// </summary>
    public class ReceivableSyncWorker : BackgroundService
    {
        private readonly ReceivableSyncQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReceivableOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ReceivableSyncWorker> _logger;

        public ReceivableSyncWorker(ReceivableSyncQueue queue, IServiceScopeFactory scopeFactory, IOptions<ReceivableOptions> options,
            TimeProvider timeProvider, ILogger<ReceivableSyncWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>ช่วงวันออกบิล: ตั้งแต่ 00:00 ของ (วันนี้ − daysBack) ถึงพรุ่งนี้ 00:00</summary>
        public static (DateTime From, DateTime To) Window(DateTime now, int daysBack) => (now.Date.AddDays(-daysBack), now.Date.AddDays(1));

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => Task.WhenAll(ProcessQueueAsync(stoppingToken), ScheduleNightlyAsync(stoppingToken));

        private async Task ProcessQueueAsync(CancellationToken stoppingToken)
        {
            await foreach (var daysBack in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var started = DateTime.Now;
                _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.RUNNING, started, "กำลังอ่านบิลเครดิตจาก HIS", started, DaysBack: daysBack));
                var progress = new SyncProgress<int>(rows =>
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.RUNNING, DateTime.Now, "กำลังอ่านบิลเครดิตจาก HIS", started, rows, daysBack)));

                using var scope = _scopeFactory.CreateScope();
                var alerts = scope.ServiceProvider.GetService<ITelegramNotifier>();
                try
                {
                    var (from, to) = Window(ThaiClock.Now(_timeProvider), daysBack);
                    var result = await scope.ServiceProvider.GetRequiredService<ReceivableSyncService>().SyncAsync(from, to, progress, stoppingToken);
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.DONE, DateTime.Now,
                        $"บิล {result.SourceRows:N0} · ใหม่ {result.Added:N0} · เปลี่ยน {result.Updated:N0} · ยกเลิก {result.Cancelled:N0}", started, result.SourceRows, daysBack, result));
                    if (alerts is not null && scope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.NotifyRefreshSuccess)
                    {
                        await alerts.SendAsync(ReceivableSyncMessages.Done(result, daysBack, DateTime.Now - started), cancellationToken: stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Receivable sync failed ({Days} days back)", daysBack);
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.FAILED, DateTime.Now, "ไม่สำเร็จ: " + ex.GetType().Name, started, DaysBack: daysBack));
                    if (alerts is not null) await alerts.SendAsync(ReceivableSyncMessages.Failed(ex.GetType().Name), "receivable-sync", stoppingToken);
                }
            }
        }

        private async Task ScheduleNightlyAsync(CancellationToken stoppingToken)
        {
            if (!_options.AutoSyncEnabled) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = ThaiClock.Now(_timeProvider);
                var next = now.Date.AddHours(Math.Clamp(_options.AutoSyncHour, 0, 23));
                if (next <= now) next = next.AddDays(1);

                try
                {
                    await Task.Delay(next - now, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                _queue.Enqueue(Math.Clamp(_options.SyncDaysBack, 1, _options.MaxSyncDaysBack));
            }
        }
    }
}
