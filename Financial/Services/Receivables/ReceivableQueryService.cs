using Financial.Data;
using Financial.Models;
using Financial.Services.Dashboard;
using Financial.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Financial.Services.Receivables
{
    /// <summary>ช่วงอายุหนี้ (วัน นับจากวันออกบิล)</summary>
    public sealed record AgingRange(string Code, string Label, int MinDays, int? MaxDays)
    {
        public bool Contains(int days) => days >= MinDays && (MaxDays is null || days <= MaxDays);
    }

    public static class AgingRanges
    {
        public static readonly IReadOnlyList<AgingRange> ALL =
        [
            new("0-30", "0–30 วัน", 0, 30),
            new("31-60", "31–60 วัน", 31, 60),
            new("61-90", "61–90 วัน", 61, 90),
            new("91-180", "91–180 วัน", 91, 180),
            new("181+", "เกิน 180 วัน", 181, null),
        ];

        public static AgingRange? Find(string? code) => ALL.FirstOrDefault(a => a.Code == code);

        public static int AgeDays(DateTime billDate, DateTime today) => Math.Max(0, (today.Date - billDate.Date).Days);
    }

    public sealed record SchemeCard(string Code, string Name, int Count, decimal Outstanding, decimal Over90);

    public sealed record AmountRow(string Label, int Count, decimal Amount, decimal Percent);

    public sealed class ReceivableOverview
    {
        public required IReadOnlyList<SchemeCard> Schemes { get; init; }
        public required string SchemeCode { get; init; }
        public required string SchemeName { get; init; }
        public bool HasZones { get; init; }
        public required IReadOnlyList<AmountRow> Aging { get; init; }
        public required IReadOnlyList<AmountRow> ByPatientType { get; init; }
        public IReadOnlyList<AmountRow> ByZone { get; init; } = [];
        public required IReadOnlyList<AmountRow> TopPayors { get; init; }
        public required IReadOnlyList<AmountRow> ByStatus { get; init; }
        public int Count { get; init; }
        public decimal Outstanding { get; init; }
        public DateTime? LastSyncedAt { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed record ReceivableListQuery(string SchemeCode, string? Side = null, string? Status = null, string? Aging = null,
        string? Payor = null, DateTime? BillFrom = null, DateTime? BillTo = null, string? Search = null, int Page = 1);

    /// <param name="VisitNumber">VN/AN — ผู้ใช้ที่ไม่ใช่ Admin เห็นแบบปิดบัง (4 ตัวท้าย)</param>
    /// <param name="Hn">HN — เฉพาะ Admin (null สำหรับผู้อื่น)</param>
    public sealed record ReceivableListItem(int Id, string BillNumber, string? VisitNumber, string? Hn, string PatientTypeCode,
        DateTime? VisitStartDate, DateTime? DischargeDate, DateTime BillDate, int AgeDays, string? PayorName, string? AgreementName,
        bool IsOutOfZone, decimal BilledAmount, decimal PaidAmount, decimal OutstandingAmount, string StatusCode);

    public sealed record ReceivableListResult(IReadOnlyList<ReceivableListItem> Items, int Total, int Page, int PageSize, decimal TotalOutstanding);

    public interface IReceivableQueryService
    {
        Task<ReceivableOverview> GetOverviewAsync(string schemeCode, CancellationToken cancellationToken = default);

        Task<ReceivableListResult> GetListAsync(ReceivableListQuery query, bool includeIdentity, int pageSize = ReceivableQueryService.PAGE_SIZE, CancellationToken cancellationToken = default);

        Task<Dictionary<string, string>> StatusNamesAsync();
    }

    /// <summary>อ่านลูกหนี้สิทธิจาก QSHC_Financial — ยอดคงค้างนับเฉพาะสถานะที่ยังต้องติดตาม (ArStatusCodes.OUTSTANDING)</summary>
    public class ReceivableQueryService : IReceivableQueryService
    {
        public const int PAGE_SIZE = 50;
        public const int EXPORT_MAX_ROWS = 50_000;
        private const int TOP_PAYORS = 10;

        private readonly ApplicationDbContext _db;
        private readonly FiscalReportOptions _fiscal;
        private readonly IReferenceValueService _referenceValues;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ReceivableQueryService> _logger;

        public ReceivableQueryService(ApplicationDbContext db, IOptions<FiscalReportOptions> fiscal, IReferenceValueService referenceValues,
            TimeProvider timeProvider, ILogger<ReceivableQueryService> logger)
        {
            _db = db;
            _fiscal = fiscal.Value;
            _referenceValues = referenceValues;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ReceivableOverview> GetOverviewAsync(string schemeCode, CancellationToken cancellationToken = default)
        {
            var today = ThaiClock.Now(_timeProvider).Date;
            var scheme = _fiscal.Scheme(schemeCode) ?? _fiscal.EffectiveSchemes[0];
            var names = await SchemeNamesAsync();
            try
            {
                var open = _db.ArReceivables.AsNoTracking().Where(r => ArStatusCodes.OUTSTANDING.Contains(r.StatusCode));
                var over90 = today.AddDays(-90);
                var perScheme = await open
                    .GroupBy(r => r.SchemeCode)
                    .Select(g => new { Code = g.Key, Count = g.Count(), Outstanding = g.Sum(r => r.OutstandingAmount), Over90 = g.Sum(r => r.BillDate < over90 ? r.OutstandingAmount : 0) })
                    .ToListAsync(cancellationToken);
                var cards = _fiscal.EffectiveSchemes.Select(s =>
                {
                    var x = perScheme.FirstOrDefault(p => p.Code == s.Code);
                    return new SchemeCard(s.Code, names.GetValueOrDefault(s.Code, s.Name), x?.Count ?? 0, x?.Outstanding ?? 0, x?.Over90 ?? 0);
                }).ToList();

                // รายละเอียดของสิทธิ์ที่เลือก — ดึงเฉพาะคอลัมน์ที่ใช้แล้วรวมในหน่วยความจำ
                var rows = await open.Where(r => r.SchemeCode == scheme.Code)
                    .Select(r => new { r.BillDate, r.OutstandingAmount, r.PatientTypeCode, r.PayorName, r.IsOutOfZone, r.StatusCode })
                    .ToListAsync(cancellationToken);
                var total = rows.Sum(r => r.OutstandingAmount);
                AmountRow Row(string label, IEnumerable<decimal> amounts) { var list = amounts.ToList(); return new(label, list.Count, list.Sum(), Percent(list.Sum(), total)); }

                var statusNames = await StatusNamesAsync();
                var lastSynced = await _db.ArReceivables.AsNoTracking().Where(r => r.SchemeCode == scheme.Code)
                    .MaxAsync(r => (DateTime?)r.LastSyncedAt, cancellationToken);

                return new ReceivableOverview
                {
                    Schemes = cards,
                    SchemeCode = scheme.Code,
                    SchemeName = names.GetValueOrDefault(scheme.Code, scheme.Name),
                    HasZones = scheme.HasZones,
                    Count = rows.Count,
                    Outstanding = total,
                    Aging = AgingRanges.ALL.Select(a => Row(a.Label, rows.Where(r => a.Contains(AgingRanges.AgeDays(r.BillDate, today))).Select(r => r.OutstandingAmount))).ToList(),
                    ByPatientType = new[] { PatientTypeCodes.OPD, PatientTypeCodes.IPD }
                        .Select(t => Row(t == PatientTypeCodes.IPD ? "ผู้ป่วยใน (IPD)" : "ผู้ป่วยนอก (OPD)", rows.Where(r => r.PatientTypeCode == t).Select(r => r.OutstandingAmount))).ToList(),
                    ByZone = scheme.HasZones
                        ? [Row("ในเขต", rows.Where(r => !r.IsOutOfZone).Select(r => r.OutstandingAmount)), Row("นอกเขต", rows.Where(r => r.IsOutOfZone).Select(r => r.OutstandingAmount))]
                        : [],
                    TopPayors = rows.GroupBy(r => r.PayorName ?? "(ไม่ระบุ)")
                        .Select(g => Row(g.Key, g.Select(r => r.OutstandingAmount)))
                        .OrderByDescending(r => r.Amount).Take(TOP_PAYORS).ToList(),
                    ByStatus = ArStatusCodes.OUTSTANDING
                        .Select(s => Row(statusNames.GetValueOrDefault(s, s), rows.Where(r => r.StatusCode == s).Select(r => r.OutstandingAmount)))
                        .Where(r => r.Count > 0).ToList(),
                    LastSyncedAt = lastSynced,
                    Warnings = lastSynced is null ? ["ยังไม่มีข้อมูลลูกหนี้ของสิทธิ์นี้ — ผู้ดูแลระบบกด \"ดึงลูกหนี้จาก HIS\" ที่หน้านี้"] : [],
                };
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException or DbUpdateException)
            {
                _logger.LogError(ex, "Cannot read receivables from QSHC_Financial");
                return new ReceivableOverview
                {
                    Schemes = _fiscal.EffectiveSchemes.Select(s => new SchemeCard(s.Code, names.GetValueOrDefault(s.Code, s.Name), 0, 0, 0)).ToList(),
                    SchemeCode = scheme.Code,
                    SchemeName = names.GetValueOrDefault(scheme.Code, scheme.Name),
                    HasZones = scheme.HasZones,
                    Aging = [], ByPatientType = [], TopPayors = [], ByStatus = [],
                    Warnings = ["อ่านข้อมูลลูกหนี้จากฐาน QSHC_Financial ไม่สำเร็จ — ตรวจการเชื่อมต่อ DefaultConnection และ migration"],
                };
            }
        }

        public async Task<ReceivableListResult> GetListAsync(ReceivableListQuery q, bool includeIdentity, int pageSize = PAGE_SIZE, CancellationToken cancellationToken = default)
        {
            var today = ThaiClock.Now(_timeProvider).Date;
            var query = _db.ArReceivables.AsNoTracking().Where(r => r.SchemeCode == q.SchemeCode);

            if (q.Side is PatientTypeCodes.OPD or PatientTypeCodes.IPD) query = query.Where(r => r.PatientTypeCode == q.Side);

            // ไม่ระบุสถานะ = เฉพาะที่ยังต้องติดตาม · "ALL" = ทุกสถานะ
            if (string.IsNullOrEmpty(q.Status)) query = query.Where(r => ArStatusCodes.OUTSTANDING.Contains(r.StatusCode));
            else if (q.Status != "ALL") query = query.Where(r => r.StatusCode == q.Status);

            if (AgingRanges.Find(q.Aging) is { } aging)
            {
                // อายุ ≥ MinDays ⇔ BillDate ≤ today − MinDays · อายุ ≤ MaxDays ⇔ BillDate ≥ today − MaxDays
                var latest = today.AddDays(-aging.MinDays + 1);
                query = query.Where(r => r.BillDate < latest);
                if (aging.MaxDays is int max)
                {
                    var earliest = today.AddDays(-max);
                    query = query.Where(r => r.BillDate >= earliest);
                }
            }
            if (!string.IsNullOrWhiteSpace(q.Payor)) query = query.Where(r => r.PayorName != null && r.PayorName.Contains(q.Payor.Trim()));
            if (q.BillFrom is { } from) query = query.Where(r => r.BillDate >= from.Date);
            if (q.BillTo is { } to) query = query.Where(r => r.BillDate < to.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                // ค้นด้วยเลขบิล หรือ VN/AN ที่พิมพ์ครบ (ไม่ค้นบางส่วนของ VN/AN — กันการไล่หาผู้ป่วย)
                var s = q.Search.Trim();
                query = query.Where(r => r.BillNumber.Contains(s) || r.VisitNumber == s);
            }

            var total = await query.CountAsync(cancellationToken);
            var totalOutstanding = total == 0 ? 0 : await query.SumAsync(r => r.OutstandingAmount, cancellationToken);
            var page = Math.Max(1, q.Page);
            var rows = await query.OrderBy(r => r.BillDate).ThenBy(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new
                {
                    r.Id, r.BillNumber, r.VisitNumber, r.Hn, r.PatientTypeCode, r.VisitStartDate, r.DischargeDate, r.BillDate,
                    r.PayorName, r.AgreementName, r.IsOutOfZone, r.BilledAmount, r.PaidAmount, r.OutstandingAmount, r.StatusCode,
                })
                .ToListAsync(cancellationToken);

            var items = rows.Select(r => new ReceivableListItem(r.Id, r.BillNumber,
                includeIdentity ? r.VisitNumber : Mask(r.VisitNumber), includeIdentity ? r.Hn : null,
                r.PatientTypeCode, r.VisitStartDate, r.DischargeDate, r.BillDate, AgingRanges.AgeDays(r.BillDate, today),
                r.PayorName, r.AgreementName, r.IsOutOfZone, r.BilledAmount, r.PaidAmount, r.OutstandingAmount, r.StatusCode)).ToList();
            return new ReceivableListResult(items, total, page, pageSize, totalOutstanding);
        }

        /// <summary>ปิดบัง VN/AN เหลือ 4 ตัวท้าย</summary>
        public static string? Mask(string? value)
            => string.IsNullOrEmpty(value) ? value : value.Length <= 4 ? new string('•', value.Length) : new string('•', value.Length - 4) + value[^4..];

        public async Task<Dictionary<string, string>> StatusNamesAsync()
        {
            var names = await NamesAsync(ReferenceCategories.AR_STATUS);
            foreach (var (code, name) in ArStatusCodes.DEFAULT_NAMES) names.TryAdd(code, name);
            return names;
        }

        private Task<Dictionary<string, string>> SchemeNamesAsync() => NamesAsync(ReferenceCategories.PAYOR_SCHEME);

        /// <summary>ชื่อจาก ReferenceValues (แก้ได้ที่หน้า ค่าอ้างอิง) — อ่านไม่ได้ใช้รหัส/ชื่อสำรองแทน</summary>
        private async Task<Dictionary<string, string>> NamesAsync(string category)
        {
            try
            {
                var values = await _referenceValues.GetByCategoryAsync(category);
                return values.GroupBy(v => v.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().NameTH, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot read ReferenceValues {Category}", category);
                return new(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole * 100, 2);
    }
}
