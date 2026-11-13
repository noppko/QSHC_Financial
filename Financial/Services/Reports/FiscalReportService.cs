using Financial.Data;
using Financial.Models;
using Financial.Services.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;

namespace Financial.Services.Reports
{
    /// <summary>
    /// ดึงข้อมูล 1 ปีงบจาก HIS HealthObject (อ่านอย่างเดียว — Database/his/queries/report-fiscal-payor.sql)
    /// แล้วรวมเป็นผลสรุปทันทีขณะอ่าน (ไม่เก็บแถวระดับผู้ป่วย)
    /// </summary>
    public interface IFiscalHisSource
    {
        /// <param name="rowsRead">รายงานจำนวนแถวที่อ่านแล้ว (สำหรับ progress)</param>
        Task<FiscalYearData> LoadYearAsync(FiscalYear year, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default);
    }

    public class FiscalHisSource : IFiscalHisSource
    {
        public const string PAYOR_CATEGORIES_PLACEHOLDER = "/*PAYOR_CATEGORIES*/";
        public const string PAYOR_NAME_FILTER_PLACEHOLDER = "/*PAYOR_NAME_FILTER*/";
        private const int PROGRESS_EVERY_ROWS = 5_000;

        private readonly string? _connectionString;
        private readonly FiscalReportOptions _options;

        public FiscalHisSource(IConfiguration configuration, IOptions<FiscalReportOptions> options)
        {
            _connectionString = configuration.GetConnectionString("HealthObject");
            _options = options.Value;
        }

        public async Task<FiscalYearData> LoadYearAsync(FiscalYear year, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("ยังไม่ได้ตั้งค่า ConnectionStrings:HealthObject");
            }

            var categories = _options.AllPayorCategoryCodes;
            var keywords = _options.AllPayorNameKeywords;
            if (categories.Count == 0) throw new InvalidOperationException("ยังไม่ได้ตั้งค่ากลุ่มผู้จ่ายของสิทธิ์ (Reports:Fiscal:Schemes)");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(BuildSql(categories.Count, keywords.Count), conn) { CommandTimeout = _options.CommandTimeoutSeconds };
            cmd.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = year.Start });
            cmd.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = year.End });
            AddPayorParameters(cmd, categories, keywords);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var aggregator = new FiscalYearAggregator(year.BuddhistYear, _options);
            ReadRows(reader, aggregator, rowsRead, cancellationToken);
            rowsRead?.Report(aggregator.Rows);
            return aggregator.Result(DateTime.Now);
        }

        internal static string BuildSql(int categoryCount, int keywordCount = 0)
            => ApplyPayorFilter(HisQueries.FiscalPayor, categoryCount, keywordCount);

        /// <summary>แทน placeholder กลุ่มผู้จ่าย/คำในชื่อ ด้วยชื่อพารามิเตอร์ (ใช้ร่วมกับ SQL ลูกหนี้สิทธิ)</summary>
        internal static string ApplyPayorFilter(string sql, int categoryCount, int keywordCount)
            => sql
                .Replace(PAYOR_CATEGORIES_PLACEHOLDER, string.Join(", ", Enumerable.Range(0, categoryCount).Select(i => $"@c{i}")))
                .Replace(PAYOR_NAME_FILTER_PLACEHOLDER, string.Concat(Enumerable.Range(0, keywordCount).Select(i => $" OR PD.PayorName LIKE @k{i} OR PA.Name LIKE @k{i}")));

        /// <summary>พารามิเตอร์ @c0.. (PYRACAT) และ @k0.. (LIKE แบบมีพารามิเตอร์ ไม่ต่อสตริง — escape อักขระพิเศษของ LIKE)</summary>
        internal static void AddPayorParameters(SqlCommand cmd, IReadOnlyList<string> categories, IReadOnlyList<string> keywords)
        {
            for (var i = 0; i < categories.Count; i++)
            {
                cmd.Parameters.Add(new SqlParameter($"@c{i}", SqlDbType.VarChar, 20) { Value = categories[i] });
            }
            for (var i = 0; i < keywords.Count; i++)
            {
                var escaped = keywords[i].Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                cmd.Parameters.Add(new SqlParameter($"@k{i}", SqlDbType.NVarChar, 210) { Value = $"%{escaped}%" });
            }
        }

        internal static void ReadRows(IDataReader r, FiscalYearAggregator aggregator, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            int side = r.GetOrdinal("BillSide"), enc = r.GetOrdinal("VisitEncounterCode"), cat = r.GetOrdinal("PayorCategoryCode"),
                name = r.GetOrdinal("AgreementName"), patient = r.GetOrdinal("PatientUid"), visits = r.GetOrdinal("Visits"),
                bills = r.GetOrdinal("Bills"), amount = r.GetOrdinal("NetAmount");
            var payor = Enumerable.Range(0, r.FieldCount).FirstOrDefault(i => r.GetName(i) == "PayorName", -1);
            while (r.Read())
            {
                aggregator.Add(new FiscalPatientRow(
                    r.GetString(side).Trim(),
                    r.IsDBNull(enc) ? null : r.GetString(enc).Trim(),
                    r.IsDBNull(cat) ? "?" : r.GetString(cat).Trim(),
                    r.IsDBNull(name) ? "(ไม่ระบุ)" : r.GetString(name).Trim(),
                    r.IsDBNull(patient) ? 0 : Convert.ToInt64(r.GetValue(patient)),
                    Convert.ToInt32(r.GetValue(visits)),
                    Convert.ToInt32(r.GetValue(bills)),
                    r.IsDBNull(amount) ? 0m : Convert.ToDecimal(r.GetValue(amount)),
                    payor >= 0 && !r.IsDBNull(payor) ? r.GetString(payor).Trim() : null));

                if (aggregator.Rows % PROGRESS_EVERY_ROWS == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowsRead?.Report(aggregator.Rows);
                }
            }
        }
    }

    public interface IFiscalReportService
    {
        Task<FiscalReport> GetAsync(string schemeCode, string side, IReadOnlyList<FiscalYear> years, CancellationToken cancellationToken = default);

        /// <summary>ปีงบ (พ.ศ.) ที่มีข้อมูลของสิทธิ์นี้ — ไม่มีเลยใช้ปีที่ประมวลผลแล้วของทุกสิทธิ์ · เรียงจากน้อยไปมาก</summary>
        Task<IReadOnlyList<int>> GetAvailableYearsAsync(string schemeCode, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// รายงานสิทธิ์ตามปีงบประมาณ — อ่านจากตาราง FiscalSchemeTotals / FiscalPayorSummaries ใน QSHC_Financial
    /// ซึ่งประมวลผลจาก HIS ไว้ล่วงหน้า (ปุ่ม "ดึงข้อมูลจาก HIS" หรืออัตโนมัติทุกคืน)
    /// </summary>
    public class FiscalReportService : IFiscalReportService
    {
        private readonly ApplicationDbContext _db;
        private readonly FiscalReportOptions _options;
        private readonly IReferenceValueService _referenceValues;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<FiscalReportService> _logger;

        public FiscalReportService(ApplicationDbContext db, IOptions<FiscalReportOptions> options, IReferenceValueService referenceValues,
            TimeProvider timeProvider, ILogger<FiscalReportService> logger)
        {
            _db = db;
            _options = options.Value;
            _referenceValues = referenceValues;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<FiscalReport> GetAsync(string schemeCode, string side, IReadOnlyList<FiscalYear> years, CancellationToken cancellationToken = default)
        {
            var now = ThaiClock.Now(_timeProvider);
            var scheme = _options.Scheme(schemeCode) ?? throw new ArgumentException($"ไม่รู้จักสิทธิ์ {schemeCode}", nameof(schemeCode));
            var schemeName = await _referenceValues.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, scheme.Code, scheme.Name);
            var warnings = new List<string>();

            try
            {
                int from = years[0].BuddhistYear, to = years[^1].BuddhistYear;
                var totals = await _db.FiscalSchemeTotals.AsNoTracking()
                    .Where(t => t.FiscalYear >= from && t.FiscalYear <= to && t.SchemeCode == scheme.Code)
                    .ToListAsync(cancellationToken);
                var agreements = await _db.FiscalPayorSummaries.AsNoTracking()
                    .Where(t => t.FiscalYear >= from && t.FiscalYear <= to && t.SchemeCode == scheme.Code && t.PatientTypeCode == side)
                    .ToListAsync(cancellationToken);

                // ปีที่ยังไม่เคยประมวลผล (ประมวลผลแล้วแต่ไม่มีข้อมูลของสิทธิ์นี้ จะมีแถวของสิทธิ์อื่นในปีนั้น)
                var processed = await _db.FiscalSchemeTotals.AsNoTracking()
                    .Where(t => t.FiscalYear >= from && t.FiscalYear <= to)
                    .Select(t => t.FiscalYear).Distinct().ToListAsync(cancellationToken);
                var missing = years.Where(y => !processed.Contains(y.BuddhistYear)).Select(y => y.Label).ToList();
                if (missing.Count > 0)
                {
                    warnings.Add($"ยังไม่มีข้อมูลปีงบ {string.Join(", ", missing)} — ให้ผู้ดูแลระบบดึงข้อมูลจาก HIS ที่หน้า ตั้งค่าระบบ");
                }

                DateTime? refreshedAt = totals.Count == 0 ? null : totals.Min(t => t.RefreshedAt);
                var available = await GetAvailableYearsAsync(scheme.Code, cancellationToken);
                return FiscalReportBuilder.Build(scheme, schemeName, side, years, totals, agreements, _options, now, warnings, refreshedAt, available);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or DbUpdateException)
            {
                _logger.LogError(ex, "Cannot read fiscal summaries from QSHC_Financial");
                warnings.Add("อ่านผลสรุปจากฐาน QSHC_Financial ไม่สำเร็จ — ตรวจการเชื่อมต่อ DefaultConnection และ migration");
                return FiscalReportBuilder.Build(scheme, schemeName, side, years, [], [], _options, now, warnings);
            }
        }

        public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(string schemeCode, CancellationToken cancellationToken = default)
        {
            try
            {
                var mine = await _db.FiscalSchemeTotals.AsNoTracking()
                    .Where(t => t.SchemeCode == schemeCode)
                    .Select(t => t.FiscalYear).Distinct().ToListAsync(cancellationToken);
                if (mine.Count == 0)
                {
                    mine = await _db.FiscalSchemeTotals.AsNoTracking()
                        .Select(t => t.FiscalYear).Distinct().ToListAsync(cancellationToken);
                }
                return mine.Order().ToList();
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or DbUpdateException)
            {
                _logger.LogError(ex, "Cannot read fiscal years from QSHC_Financial");
                return [];
            }
        }
    }
}
