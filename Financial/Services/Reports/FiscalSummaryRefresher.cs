using Financial.Data;
using Financial.Services.Dashboard;
using Financial.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Financial.Services.Reports
{
    /// <summary>
    /// ประมวลผลรายงานสิทธิ์ 1 ปีงบ (ทุกสิทธิ์ในครั้งเดียว): ดึงจาก HIS → แทนที่ผลของปีนั้นใน FiscalSchemeTotals / FiscalPayorSummaries (QSHC_Financial)
    /// </summary>
    public class FiscalSummaryRefresher
    {
        private readonly ApplicationDbContext _db;
        private readonly IFiscalHisSource _hisSource;
        private readonly ILogger<FiscalSummaryRefresher> _logger;

        public FiscalSummaryRefresher(ApplicationDbContext db, IFiscalHisSource hisSource, ILogger<FiscalSummaryRefresher> logger)
        {
            _db = db;
            _hisSource = hisSource;
            _logger = logger;
        }

        /// <returns>จำนวนแถวที่อ่านจาก HIS</returns>
        public async Task<int> RefreshAsync(FiscalYear year, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            // ส่วนที่ใช้เวลานาน (query HIS) ทำก่อนเปิดทรานแซกชัน เพื่อไม่ให้ล็อกตารางสรุปนาน
            var data = await _hisSource.LoadYearAsync(year, rowsRead, cancellationToken);

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

                // ผลสรุปเป็นข้อมูลที่คำนวณซ้ำได้ — ลบของเดิมของปีนั้นจริง (ไม่ใช่ soft delete) เพื่อไม่ให้ตารางโตเรื่อยๆ
                _db.FiscalSchemeTotals.RemoveRange(await _db.FiscalSchemeTotals.IgnoreQueryFilters().Where(s => s.FiscalYear == year.BuddhistYear).ToListAsync(cancellationToken));
                _db.FiscalPayorSummaries.RemoveRange(await _db.FiscalPayorSummaries.IgnoreQueryFilters().Where(s => s.FiscalYear == year.BuddhistYear).ToListAsync(cancellationToken));
                _db.FiscalSchemeTotals.AddRange(data.Totals);
                _db.FiscalPayorSummaries.AddRange(data.Agreements);

                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            });
            _db.ChangeTracker.Clear();

            // จำนวนผู้ป่วยต่อสิทธิ์ — ใช้ตรวจว่าสิทธิ์ใดไม่มีข้อมูลเลย (เช่น ตั้งกลุ่มผู้จ่ายไม่ตรงกับ HIS)
            var perScheme = string.Join(", ", data.Totals.Where(t => t.ZoneCode == Models.FiscalZoneCodes.ALL)
                .GroupBy(t => t.SchemeCode).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Sum(t => t.Patients):N0}"));
            _logger.LogInformation("Fiscal summary FY {Year} refreshed: {Rows} source rows, {Totals} totals, {Agreements} agreements, patients by scheme: {Schemes}",
                year.BuddhistYear, data.SourceRows, data.Totals.Count, data.Agreements.Count, perScheme.Length == 0 ? "(none)" : perScheme);
            return data.SourceRows;
        }
    }

    /// <summary>สถานะการประมวลผลของปีงบ (ใช้แสดง progress bar)</summary>
    public sealed record RefreshStatus(int FiscalYear, string State, DateTime UpdatedAt, string? Message = null, DateTime? StartedAt = null, int RowsRead = 0)
    {
        public const string QUEUED = "QUEUED";
        public const string RUNNING = "RUNNING";
        public const string DONE = "DONE";
        public const string FAILED = "FAILED";

        public bool IsActive => State is QUEUED or RUNNING;
    }

    /// <summary>
    /// คิวงานประมวลผล (ใช้ร่วมกันทั้งแอป) — สั่งจากหน้าเว็บแล้วทำงานเบื้องหลังทีละปี ไม่ต้องรอหน้าเว็บ
    /// </summary>
    public class FiscalRefreshQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
        private readonly ConcurrentDictionary<int, RefreshStatus> _status = new();

        public ChannelReader<int> Reader => _channel.Reader;

        /// <returns>false ถ้าปีนี้อยู่ในคิว/กำลังประมวลผลอยู่แล้ว</returns>
        public bool Enqueue(int fiscalYear)
        {
            if (_status.TryGetValue(fiscalYear, out var s) && s.IsActive)
            {
                return false;
            }

            _status[fiscalYear] = new RefreshStatus(fiscalYear, RefreshStatus.QUEUED, DateTime.Now);
            return _channel.Writer.TryWrite(fiscalYear);
        }

        public void SetStatus(RefreshStatus status) => _status[status.FiscalYear] = status;

        public RefreshStatus? Get(int fiscalYear) => _status.TryGetValue(fiscalYear, out var s) ? s : null;

        public IReadOnlyList<RefreshStatus> Statuses => _status.Values.OrderByDescending(s => s.FiscalYear).ToList();
    }

    /// <summary>ข้อความแจ้งเตือนของ Worker ดึงข้อมูลปีงบ — ไม่มีข้อมูลผู้ป่วย ไม่มีข้อความ exception (อาจมีชื่อเซิร์ฟเวอร์/SQL)</summary>
    public static class RefreshMessages
    {
        public static string Done(int fiscalYear, int sourceRows, TimeSpan elapsed)
            => $"✅ ดึงข้อมูลรายงานสิทธิ์ปีงบ {fiscalYear} จาก HIS สำเร็จ ({sourceRows:N0} แถว, ใช้เวลา {(int)elapsed.TotalMinutes} นาที {elapsed.Seconds} วินาที)";

        public static string Failed(int fiscalYear, string exceptionType)
            => $"❌ ดึงข้อมูลรายงานสิทธิ์ปีงบ {fiscalYear} จาก HIS ไม่สำเร็จ ({exceptionType}) — ดูรายละเอียดใน log ของเซิร์ฟเวอร์";
    }

    /// <summary>IProgress ที่เรียก callback ทันทีบน thread เดียวกัน (ลำดับไม่สลับ)</summary>
    public sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    /// <summary>
    /// งานเบื้องหลัง: ทำตามคิว และประมวลผลอัตโนมัติทุกคืน (Reports:Fiscal:AutoRefreshEnabled / AutoRefreshHour)
    /// </summary>
    public class FiscalRefreshWorker : BackgroundService
    {
        private readonly FiscalRefreshQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FiscalReportOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<FiscalRefreshWorker> _logger;

        public FiscalRefreshWorker(FiscalRefreshQueue queue, IServiceScopeFactory scopeFactory, IOptions<FiscalReportOptions> options,
            TimeProvider timeProvider, ILogger<FiscalRefreshWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => Task.WhenAll(ProcessQueueAsync(stoppingToken), ScheduleNightlyAsync(stoppingToken));

        private async Task ProcessQueueAsync(CancellationToken stoppingToken)
        {
            await foreach (var year in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var started = DateTime.Now;
                _queue.SetStatus(new RefreshStatus(year, RefreshStatus.RUNNING, started, "กำลังดึงข้อมูลจาก HIS", started));
                // อ่านแถวจาก HIS ไปเรื่อยๆ → อัปเดตจำนวนแถวให้หน้าเว็บเห็นความคืบหน้า
                // Progress<T> ส่งผลแบบ async อาจมาถึงหลังสถานะ DONE — ใช้แบบเรียกทันทีแทน
                var progress = new SyncProgress<int>(rows =>
                    _queue.SetStatus(new RefreshStatus(year, RefreshStatus.RUNNING, DateTime.Now, "กำลังอ่านข้อมูลจาก HIS", started, rows)));
                using var scope = _scopeFactory.CreateScope();
                var alerts = scope.ServiceProvider.GetService<ITelegramNotifier>();
                try
                {
                    var refresher = scope.ServiceProvider.GetRequiredService<FiscalSummaryRefresher>();
                    var count = await refresher.RefreshAsync(new FiscalYear(year), progress, stoppingToken);
                    _queue.SetStatus(new RefreshStatus(year, RefreshStatus.DONE, DateTime.Now, $"อ่าน {count:N0} แถว", started, count));
                    if (alerts is not null && scope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.NotifyRefreshSuccess)
                    {
                        await alerts.SendAsync(RefreshMessages.Done(year, count, DateTime.Now - started), cancellationToken: stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fiscal summary FY {Year} failed", year);
                    _queue.SetStatus(new RefreshStatus(year, RefreshStatus.FAILED, DateTime.Now, "ไม่สำเร็จ: " + ex.GetType().Name, started));
                    if (alerts is not null) await alerts.SendAsync(RefreshMessages.Failed(year, ex.GetType().Name), cancellationToken: stoppingToken);
                }
            }
        }

        private async Task ScheduleNightlyAsync(CancellationToken stoppingToken)
        {
            if (!_options.AutoRefreshEnabled) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = ThaiClock.Now(_timeProvider);
                var next = now.Date.AddHours(Math.Clamp(_options.AutoRefreshHour, 0, 23));
                if (next <= now) next = next.AddDays(1);

                try
                {
                    await Task.Delay(next - now, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                foreach (var year in YearsToRefresh(ThaiClock.Now(_timeProvider)))
                {
                    _queue.Enqueue(year);
                }
            }
        }

        /// <summary>ปีงบปัจจุบัน และปีงบที่เพิ่งปิดในช่วง ต.ค.–ธ.ค. (บิลของปีก่อนยังเข้ามาได้)</summary>
        public static IReadOnlyList<int> YearsToRefresh(DateTime now)
        {
            var current = FiscalYear.Of(now).BuddhistYear;
            return now.Month is 10 or 11 or 12 ? [current, current - 1] : [current];
        }
    }
}
