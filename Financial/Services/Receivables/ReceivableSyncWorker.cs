using Financial.Data;
using Financial.Services.Dashboard;
using Financial.Services.Notifications;
using Financial.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Financial.Services.Receivables
{
    /// <summary>สถานะงานดึงลูกหนี้ (ทีละงาน) — ใช้แสดงความคืบหน้าบนหน้าเว็บ</summary>
    /// <param name="From">ดึงบิลตั้งแต่วันที่ (วันออกบิล)</param>
    /// <param name="To">ถึงวันที่ (รวมวันนั้น) — null = ถึงวันนี้</param>
    public sealed record ReceivableSyncStatus(string State, DateTime UpdatedAt, string? Message = null, DateTime? StartedAt = null, int RowsRead = 0,
        DateTime? From = null, ReceivableSyncResult? Result = null, DateTime? To = null)
    {
        public bool IsActive => State is RefreshStatus.QUEUED or RefreshStatus.RUNNING;
    }

    /// <summary>ช่วงวันออกบิลที่สั่งดึง (To รวมวันนั้น, null = ถึงวันนี้)</summary>
    public readonly record struct ReceivableSyncRequest(DateTime From, DateTime? To);

    /// <summary>คิวงานดึงลูกหนี้จาก HIS (ใช้ร่วมทั้งแอป) — รับครั้งละ 1 งาน</summary>
    public class ReceivableSyncQueue
    {
        private readonly Channel<ReceivableSyncRequest> _channel = Channel.CreateUnbounded<ReceivableSyncRequest>();
        private ReceivableSyncStatus? _status;
        private readonly Lock _lock = new();

        public ChannelReader<ReceivableSyncRequest> Reader => _channel.Reader;

        public ReceivableSyncStatus? Status => _status;

        /// <returns>false ถ้ามีงานอยู่ในคิว/กำลังทำ</returns>
        public bool Enqueue(DateTime from, DateTime? to = null)
        {
            lock (_lock)
            {
                if (_status?.IsActive == true) return false;
                _status = new ReceivableSyncStatus(RefreshStatus.QUEUED, DateTime.Now, "รอดำเนินการ", From: from.Date, To: to?.Date);
                return _channel.Writer.TryWrite(new ReceivableSyncRequest(from.Date, to?.Date));
            }
        }

        public void SetStatus(ReceivableSyncStatus status) => _status = status;
    }

    public static class ReceivableSyncMessages
    {
        public static string Done(ReceivableSyncResult r, DateTime from, TimeSpan elapsed)
            => $"✅ ดึงลูกหนี้สิทธิจาก HIS ตั้งแต่ {from:dd/MM/yyyy} สำเร็จ: บิล {r.SourceRows:N0}, ใหม่ {r.Added:N0}, เปลี่ยน {r.Updated:N0}, ยกเลิก {r.Cancelled:N0}, ซ้ำ {r.Duplicates:N0} ({(int)elapsed.TotalMinutes} นาที {elapsed.Seconds} วินาที)";

        public static string Failed(string exceptionType)
            => $"❌ ดึงลูกหนี้สิทธิจาก HIS ไม่สำเร็จ ({exceptionType}) — ดูรายละเอียดใน log ของเซิร์ฟเวอร์";
    }

    /// <summary>
    /// งานเบื้องหลัง: ทำตามคิว (ปุ่มดึงข้อมูล), ดึงครั้งแรกทันทีถ้ายังไม่มีลูกหนี้ (ตั้งแต่ StartDate)
    /// และดึงอัตโนมัติทุกคืน (Receivables:AutoSyncEnabled / AutoSyncHour / SyncDaysBack — ไม่ก่อน StartDate)
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

        /// <summary>
        /// ช่วงวันออกบิล [From, To): ตั้งแต่ from (ไม่ก่อนวันเริ่มตั้งหนี้) ถึงสิ้นวัน to (ไม่เกินวันนี้; null = วันนี้)
        /// </summary>
        public static (DateTime From, DateTime To) Window(DateTime now, DateTime from, DateTime startDate, DateTime? to = null)
        {
            var start = from.Date < startDate.Date ? startDate.Date : from.Date;
            var end = (to?.Date ?? now.Date) > now.Date ? now.Date : (to?.Date ?? now.Date);
            if (end < start) end = start;
            return (start, end.AddDays(1));
        }

        /// <summary>รอบกลางคืน: ย้อนหลัง SyncDaysBack วัน แต่ไม่ก่อนวันเริ่มตั้งหนี้</summary>
        public static DateTime NightlyFrom(DateTime now, ReceivableOptions options)
        {
            var start = options.EffectiveStartDate(now);
            var back = now.Date.AddDays(-Math.Max(1, options.SyncDaysBack));
            return back < start ? start : back;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => Task.WhenAll(ProcessQueueAsync(stoppingToken), ScheduleNightlyAsync(stoppingToken), InitialSyncAsync(stoppingToken));

        private async Task InitialSyncAsync(CancellationToken stoppingToken)
        {
            if (!_options.AutoSyncEnabled || !_options.InitialSyncOnStartup) return;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.ArReceivables.AnyAsync(stoppingToken)) return;

                var from = _options.EffectiveStartDate(ThaiClock.Now(_timeProvider));
                _logger.LogInformation("No receivables yet — initial sync from {From:yyyy-MM-dd}", from);
                _queue.Enqueue(from);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot check receivables for initial sync");
            }
        }

        private async Task ProcessQueueAsync(CancellationToken stoppingToken)
        {
            await foreach (var requested in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var started = DateTime.Now;
                var now = ThaiClock.Now(_timeProvider);
                var (from, to) = Window(now, requested.From, _options.EffectiveStartDate(now), requested.To);
                var last = to.AddDays(-1);
                _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.RUNNING, started, "กำลังอ่านบิลเครดิตจาก HIS", started, From: from, To: last));
                var progress = new SyncProgress<int>(rows =>
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.RUNNING, DateTime.Now, "กำลังอ่านบิลเครดิตจาก HIS", started, rows, from, To: last)));

                using var scope = _scopeFactory.CreateScope();
                var alerts = scope.ServiceProvider.GetService<ITelegramNotifier>();
                try
                {
                    var result = await scope.ServiceProvider.GetRequiredService<ReceivableSyncService>().SyncAsync(from, to, progress, stoppingToken);
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.DONE, DateTime.Now,
                        $"{from:dd/MM/yyyy}–{last:dd/MM/yyyy} · บิล {result.SourceRows:N0} · ใหม่ {result.Added:N0} · เปลี่ยน {result.Updated:N0} · ยกเลิก {result.Cancelled:N0} · ซ้ำ (ไม่บันทึก) {result.Duplicates:N0}", started, result.SourceRows, from, result, last));
                    if (alerts is not null && scope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.NotifyRefreshSuccess)
                    {
                        await alerts.SendAsync(ReceivableSyncMessages.Done(result, from, DateTime.Now - started), cancellationToken: stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Receivable sync failed (from {From:yyyy-MM-dd})", from);
                    _queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.FAILED, DateTime.Now, "ไม่สำเร็จ: " + ex.GetType().Name, started, From: from, To: last));
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

                _queue.Enqueue(NightlyFrom(ThaiClock.Now(_timeProvider), _options));
            }
        }
    }
}
