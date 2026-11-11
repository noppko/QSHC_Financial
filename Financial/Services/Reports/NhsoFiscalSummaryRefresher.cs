using Financial.Data;
using Financial.Models;
using Financial.Services.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Financial.Services.Reports
{
    /// <summary>
    /// ประมวลผลรายงาน สปสช. 1 ปีงบ: ดึงตัวเลขรวมจาก HIS → แทนที่แถวของปีนั้นในตาราง NhsoFiscalSummaries (QSHC_Financial)
    /// </summary>
    public class NhsoFiscalSummaryRefresher
    {
        private readonly ApplicationDbContext _db;
        private readonly INhsoHisSource _hisSource;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NhsoFiscalSummaryRefresher> _logger;

        public NhsoFiscalSummaryRefresher(ApplicationDbContext db, INhsoHisSource hisSource, IMemoryCache cache, ILogger<NhsoFiscalSummaryRefresher> logger)
        {
            _db = db;
            _hisSource = hisSource;
            _cache = cache;
            _logger = logger;
        }

        /// <returns>จำนวนแถวที่บันทึก</returns>
        public async Task<int> RefreshAsync(FiscalYear year, CancellationToken cancellationToken = default)
        {
            // ส่วนที่ใช้เวลานาน (query HIS) ทำก่อนเปิดทรานแซกชัน เพื่อไม่ให้ล็อกตารางสรุปนาน
            var rows = await _hisSource.LoadYearAsync(year, cancellationToken);
            var refreshedAt = DateTime.Now;

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

                // ผลสรุปเป็นข้อมูลที่คำนวณซ้ำได้ — ลบของเดิมของปีนั้นจริง (ไม่ใช่ soft delete) เพื่อไม่ให้ตารางโตเรื่อยๆ
                var old = await _db.NhsoFiscalSummaries.IgnoreQueryFilters().Where(s => s.FiscalYear == year.BuddhistYear).ToListAsync(cancellationToken);
                _db.NhsoFiscalSummaries.RemoveRange(old);

                _db.NhsoFiscalSummaries.AddRange(rows.Select(r => new NhsoFiscalSummary
                {
                    FiscalYear = year.BuddhistYear,
                    PatientTypeCode = PatientTypeCodes.Normalize(r.Side),
                    AgreementEncounterCode = r.AgreementEncounterCode,
                    PayorCategoryCode = r.PayorCategoryCode,
                    AgreementName = r.AgreementName.Length > 400 ? r.AgreementName[..400] : r.AgreementName,
                    Visits = r.Visits,
                    Bills = r.Bills,
                    NetAmount = r.NetAmount,
                    RefreshedAt = refreshedAt,
                }));

                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            });

            _cache.Remove($"report:nhso:{year.BuddhistYear}");
            _logger.LogInformation("NHSO fiscal summary FY {Year} refreshed: {Rows} rows", year.BuddhistYear, rows.Count);
            return rows.Count;
        }
    }

    public sealed record RefreshStatus(int FiscalYear, string State, DateTime UpdatedAt, string? Message = null)
    {
        public const string QUEUED = "QUEUED";
        public const string RUNNING = "RUNNING";
        public const string DONE = "DONE";
        public const string FAILED = "FAILED";
    }

    /// <summary>
    /// คิวงานประมวลผล (ใช้ร่วมกันทั้งแอป) — ผู้ดูแลระบบกดสั่งจากหน้าเว็บแล้วทำงานเบื้องหลัง ไม่ต้องรอหน้าเว็บ
    /// </summary>
    public class NhsoFiscalRefreshQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
        private readonly ConcurrentDictionary<int, RefreshStatus> _status = new();

        public ChannelReader<int> Reader => _channel.Reader;

        /// <returns>false ถ้าปีนี้อยู่ในคิว/กำลังประมวลผลอยู่แล้ว</returns>
        public bool Enqueue(int fiscalYear)
        {
            if (_status.TryGetValue(fiscalYear, out var s) && s.State is RefreshStatus.QUEUED or RefreshStatus.RUNNING)
            {
                return false;
            }

            _status[fiscalYear] = new RefreshStatus(fiscalYear, RefreshStatus.QUEUED, DateTime.Now);
            return _channel.Writer.TryWrite(fiscalYear);
        }

        public void SetStatus(RefreshStatus status) => _status[status.FiscalYear] = status;

        public IReadOnlyList<RefreshStatus> Statuses => _status.Values.OrderByDescending(s => s.FiscalYear).ToList();
    }

    /// <summary>
    /// งานเบื้องหลัง: ทำตามคิว และประมวลผลอัตโนมัติทุกคืน (Reports:Nhso:AutoRefreshEnabled / AutoRefreshHour)
    /// </summary>
    public class NhsoFiscalRefreshWorker : BackgroundService
    {
        private readonly NhsoFiscalRefreshQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NhsoReportOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<NhsoFiscalRefreshWorker> _logger;

        public NhsoFiscalRefreshWorker(NhsoFiscalRefreshQueue queue, IServiceScopeFactory scopeFactory, IOptions<NhsoReportOptions> options,
            TimeProvider timeProvider, ILogger<NhsoFiscalRefreshWorker> logger)
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
                _queue.SetStatus(new RefreshStatus(year, RefreshStatus.RUNNING, DateTime.Now));
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var refresher = scope.ServiceProvider.GetRequiredService<NhsoFiscalSummaryRefresher>();
                    var count = await refresher.RefreshAsync(new FiscalYear(year), stoppingToken);
                    _queue.SetStatus(new RefreshStatus(year, RefreshStatus.DONE, DateTime.Now, $"บันทึก {count:N0} แถว"));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NHSO fiscal summary FY {Year} failed", year);
                    _queue.SetStatus(new RefreshStatus(year, RefreshStatus.FAILED, DateTime.Now, ex.GetType().Name));
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
