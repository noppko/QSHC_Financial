using Financial.Data;
using Financial.Models;
using Financial.Services.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Data;

namespace Financial.Services.Reports
{
    public interface INhsoFiscalReportService
    {
        Task<NhsoFiscalReport> GetAsync(string side, IReadOnlyList<FiscalYear> years, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ดึงตัวเลขรวมของ 1 ปีงบจาก HIS HealthObject (อ่านอย่างเดียว — Database/his/queries/report-nhso-fiscal.sql)
    /// </summary>
    public interface INhsoHisSource
    {
        Task<IReadOnlyList<NhsoAgreementRow>> LoadYearAsync(FiscalYear year, CancellationToken cancellationToken = default);
    }

    public class NhsoHisSource : INhsoHisSource
    {
        private readonly string? _connectionString;
        private readonly NhsoReportOptions _options;

        public NhsoHisSource(IConfiguration configuration, IOptions<NhsoReportOptions> options)
        {
            _connectionString = configuration.GetConnectionString("HealthObject");
            _options = options.Value;
        }

        public async Task<IReadOnlyList<NhsoAgreementRow>> LoadYearAsync(FiscalYear year, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("ยังไม่ได้ตั้งค่า ConnectionStrings:HealthObject");
            }

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(HisQueries.NhsoFiscal, conn) { CommandTimeout = _options.CommandTimeoutSeconds };
            cmd.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = year.Start });
            cmd.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = year.End });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return ReadRows(reader, year.BuddhistYear);
        }

        internal static List<NhsoAgreementRow> ReadRows(IDataReader r, int fiscalYear)
        {
            int side = r.GetOrdinal("Side"), cat = r.GetOrdinal("PayorCategoryCode"), name = r.GetOrdinal("AgreementName"),
                visits = r.GetOrdinal("Visits"), bills = r.GetOrdinal("Bills"), amount = r.GetOrdinal("NetAmount"),
                agreementType = r.GetOrdinal("AgreementEncounterCode");
            var rows = new List<NhsoAgreementRow>();
            while (r.Read())
            {
                rows.Add(new NhsoAgreementRow(
                    fiscalYear,
                    r.GetString(side).Trim(),
                    r.IsDBNull(cat) ? "?" : r.GetString(cat),
                    r.IsDBNull(name) ? "(ไม่ระบุ)" : r.GetString(name).Trim(),
                    Convert.ToInt32(r.GetValue(visits)),
                    Convert.ToInt32(r.GetValue(bills)),
                    r.IsDBNull(amount) ? 0m : Convert.ToDecimal(r.GetValue(amount)),
                    r.IsDBNull(agreementType) ? null : r.GetString(agreementType).Trim()));
            }
            return rows;
        }
    }

    /// <summary>
    /// รายงานสิทธิ สปสช. ตามปีงบประมาณ — 2 โหมด (Reports:Nhso:Source)
    /// - "Summary" (ค่าเริ่มต้น): อ่านจากตาราง NhsoFiscalSummaries ใน QSHC_Financial ซึ่งประมวลผลไว้ล่วงหน้า (NhsoFiscalSummaryRefresher)
    /// - "Query": คำนวณจาก HIS โดยตรงทีละปีงบ (ครั้งละไม่เกิน 2 ปีพร้อมกัน), ปีที่ปิดแล้ว cache 12 ชม., ปีปัจจุบัน 30 นาที
    /// </summary>
    public class NhsoFiscalReportService : INhsoFiscalReportService
    {
        private static readonly SemaphoreSlim Throttle = new(2, 2);

        private readonly ApplicationDbContext _db;
        private readonly INhsoHisSource _hisSource;
        private readonly NhsoReportOptions _options;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<NhsoFiscalReportService> _logger;

        public NhsoFiscalReportService(ApplicationDbContext db, INhsoHisSource hisSource, IOptions<NhsoReportOptions> options,
            IMemoryCache cache, TimeProvider timeProvider, ILogger<NhsoFiscalReportService> logger)
        {
            _db = db;
            _hisSource = hisSource;
            _options = options.Value;
            _cache = cache;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<NhsoFiscalReport> GetAsync(string side, IReadOnlyList<FiscalYear> years, CancellationToken cancellationToken = default)
        {
            var now = ThaiClock.Now(_timeProvider);
            var warnings = new List<string>();

            if (_options.UseSummary)
            {
                try
                {
                    var (rows, refreshedAt) = await LoadFromSummaryAsync(years, cancellationToken);
                    var missing = years.Where(y => rows.All(r => r.FiscalYear != y.BuddhistYear)).Select(y => y.Label).ToList();
                    if (missing.Count > 0)
                    {
                        warnings.Add($"ยังไม่มีผลประมวลผลของปีงบ {string.Join(", ", missing)} — ผู้ดูแลระบบสั่งประมวลผลได้ที่หน้า ตั้งค่าระบบ");
                    }
                    return NhsoFiscalReportBuilder.Build(side, years, rows, _options, now, warnings, refreshedAt);
                }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException or DbUpdateException)
                {
                    _logger.LogError(ex, "Cannot read NhsoFiscalSummaries from QSHC_Financial");
                    warnings.Add("อ่านผลสรุปจากฐาน QSHC_Financial ไม่สำเร็จ — ตรวจการเชื่อมต่อ DefaultConnection และ migration");
                    return NhsoFiscalReportBuilder.Build(side, years, [], _options, now, warnings);
                }
            }

            var currentYear = FiscalYear.Of(now);
            var tasks = years.Select(async year =>
            {
                try
                {
                    return await LoadFromHisCachedAsync(year, year.BuddhistYear >= currentYear.BuddhistYear, cancellationToken);
                }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException)
                {
                    _logger.LogError(ex, "NHSO fiscal report failed for FY {Year}", year.BuddhistYear);
                    lock (warnings) warnings.Add($"ดึงข้อมูลปีงบ {year.Label} จาก HIS ไม่สำเร็จ");
                    return [];
                }
            }).ToList();

            var all = (await Task.WhenAll(tasks)).SelectMany(r => r).ToList();
            return NhsoFiscalReportBuilder.Build(side, years, all, _options, now, warnings);
        }

        private async Task<(IReadOnlyList<NhsoAgreementRow> Rows, DateTime? RefreshedAt)> LoadFromSummaryAsync(IReadOnlyList<FiscalYear> years, CancellationToken cancellationToken)
        {
            int from = years[0].BuddhistYear, to = years[^1].BuddhistYear;
            var summaries = await _db.NhsoFiscalSummaries
                .AsNoTracking()
                .Where(s => s.FiscalYear >= from && s.FiscalYear <= to)
                .ToListAsync(cancellationToken);

            var rows = summaries
                .Select(s => new NhsoAgreementRow(s.FiscalYear, s.PatientTypeCode, s.PayorCategoryCode, s.AgreementName, s.Visits, s.Bills, s.NetAmount, s.AgreementEncounterCode))
                .ToList();

            // แสดงเวลาที่เก่าที่สุด = ข้อมูลสดถึงอย่างน้อยเวลานั้น
            DateTime? refreshedAt = summaries.Count == 0 ? null : summaries.Min(s => s.RefreshedAt);
            return (rows, refreshedAt);
        }

        private async Task<IReadOnlyList<NhsoAgreementRow>> LoadFromHisCachedAsync(FiscalYear year, bool isCurrent, CancellationToken cancellationToken)
        {
            var key = $"report:nhso:{year.BuddhistYear}";
            if (_cache.TryGetValue(key, out IReadOnlyList<NhsoAgreementRow>? cached) && cached is not null)
            {
                return cached;
            }

            await Throttle.WaitAsync(cancellationToken);
            try
            {
                var rows = await _hisSource.LoadYearAsync(year, cancellationToken);
                _cache.Set(key, rows, isCurrent ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12));
                return rows;
            }
            finally
            {
                Throttle.Release();
            }
        }
    }
}
