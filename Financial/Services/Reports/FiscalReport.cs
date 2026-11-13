using Financial.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Financial.Services.Reports
{
    /// <summary>
    /// ปีงบประมาณ (พ.ศ.) = 1 ต.ค. ของปีก่อนหน้า ถึง 30 ก.ย. — เช่น ปีงบ 2568 = 1 ต.ค. 2567 – 30 ก.ย. 2568
    /// </summary>
    public readonly record struct FiscalYear(int BuddhistYear)
    {
        public const int BUDDHIST_OFFSET = 543;

        public DateTime Start => new(BuddhistYear - BUDDHIST_OFFSET - 1, 10, 1);

        /// <summary>วันแรกของปีงบถัดไป (exclusive)</summary>
        public DateTime End => new(BuddhistYear - BUDDHIST_OFFSET, 10, 1);

        public static FiscalYear Of(DateTime date)
            => new(date.Year + BUDDHIST_OFFSET + (date.Month >= 10 ? 1 : 0));

        public string Label => BuddhistYear.ToString(CultureInfo.InvariantCulture);

        public string RangeText
        {
            get
            {
                var th = CultureInfo.GetCultureInfo("th-TH");
                return $"{Start.Day} {th.DateTimeFormat.GetAbbreviatedMonthName(Start.Month)} {Start.Year + BUDDHIST_OFFSET} – 30 {th.DateTimeFormat.GetAbbreviatedMonthName(9)} {End.Year + BUDDHIST_OFFSET}";
            }
        }
    }

    /// <summary>สิทธิ์ 1 รายการ (appsettings.json → Reports:Fiscal:Schemes)</summary>
    public class FiscalSchemeOptions
    {
        /// <summary>รหัส ReferenceValues category PAYOR_SCHEME เช่น NHSO</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>ชื่อที่แสดง (ถ้าอ่าน ReferenceValues ไม่ได้)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>กลุ่มผู้จ่ายใน HIS (PayorDetail.PYRACATUID → PYRACAT) ที่นับเป็นสิทธิ์นี้</summary>
        public string[] PayorCategoryCodes { get; set; } = [];

        /// <summary>
        /// คำในชื่อผู้จ่าย (PayorDetail.PayorName) หรือชื่อเงื่อนไขการเรียกเก็บ (PayorAgreement.Name) ที่นับเป็นสิทธิ์นี้
        /// มาก่อนกลุ่มผู้จ่าย — เช่น อปท. = ผู้จ่าย "สปสช(อปท)" ซึ่งอยู่กลุ่ม สปสช. ใน HIS (นับใน อปท. ไม่นับซ้ำใน สปสช.)
        /// </summary>
        public string[] PayorNameKeywords { get; set; } = [];

        /// <summary>แยกในเขต/นอกเขต — ไม่ระบุ = เฉพาะ สปสช. (สิทธิ์อื่นแสดงเฉพาะยอดรวม)</summary>
        public bool? SplitZones { get; set; }

        public bool HasZones => SplitZones ?? string.Equals(Code, PayorSchemeCodes.NHSO, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// การตั้งค่ารายงานสิทธิ์ตามปีงบประมาณ (appsettings.json → Reports:Fiscal)
    /// </summary>
    public class FiscalReportOptions
    {
        public const string SECTION_NAME = "Reports:Fiscal";

        /// <summary>
        /// ค่าเริ่มต้นจาก PYRACAT ใน HIS (finance.md §5):
        /// สปสช. = A สปสช · กรมบัญชีกลาง = B, V (โรคเฉพาะ) · อปท. = P, W · ประกันสังคม = C, N (เฉพาะ), O (เหมาจ่าย)
        /// </summary>
        public static IReadOnlyList<FiscalSchemeOptions> DEFAULT_SCHEMES =>
        [
            new() { Code = PayorSchemeCodes.NHSO, Name = "สปสช.", PayorCategoryCodes = ["A"] },
            new() { Code = PayorSchemeCodes.CSMBS, Name = "กรมบัญชีกลาง", PayorCategoryCodes = ["B", "V"] },
            new() { Code = PayorSchemeCodes.SSO, Name = "ประกันสังคม", PayorCategoryCodes = ["C", "N", "O"] },
            new() { Code = PayorSchemeCodes.LGO, Name = "อปท.", PayorCategoryCodes = ["P", "W"], PayorNameKeywords = ["อปท"] },
        ];

        /// <summary>สิทธิ์ที่ทำรายงาน — ว่าง = ใช้ DEFAULT_SCHEMES (ไม่ใส่ค่าเริ่มต้นใน property เพราะ config binder จะต่อท้ายรายการ)</summary>
        public List<FiscalSchemeOptions> Schemes { get; set; } = [];

        public IReadOnlyList<FiscalSchemeOptions> EffectiveSchemes => Schemes.Count > 0 ? Schemes : DEFAULT_SCHEMES;

        /// <summary>เงื่อนไขการเรียกเก็บ (PayorAgreement.Name) ที่มีคำเหล่านี้ = นอกเขต (นอกจังหวัด); มีคำว่า "ใน" หรือไม่มีทั้งสองคำ = ในเขต</summary>
        public string[] OutOfZoneKeywords { get; set; } = [];

        public static readonly string[] DEFAULT_OUT_OF_ZONE_KEYWORDS = ["นอก"];

        /// <summary>วลีที่มีคำว่า "นอก" แต่ไม่ได้หมายถึงนอกเขต (Regular Expression) — ตัดออกก่อนตรวจ OutOfZoneKeywords</summary>
        public string[] OutOfZoneIgnorePhrases { get; set; } = [];

        public static readonly string[] DEFAULT_OUT_OF_ZONE_IGNORE = ["ผู้ป่วยนอก(?!เขต|จังหวัด|ขอนแก่น)", "นอกเวลา"];

        /// <summary>
        /// ประเภท visit ใน HIS (PatientVisit.ENTYPUID → ReferenceValue ENTYP) → O/I
        /// VN: OUPAT = OPD, AEPAT (ER) = OPD · AN: INPAT = IPD — ไม่พบประเภท visit ใช้ประเภทบิลแทน
        /// </summary>
        public Dictionary<string, string> VisitEncounterTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OUPAT"] = PatientTypeCodes.OPD,
            ["INPAT"] = PatientTypeCodes.IPD,
            ["AEPAT"] = PatientTypeCodes.OPD,
        };

        public int CommandTimeoutSeconds { get; set; } = 600;

        /// <summary>ประมวลผลอัตโนมัติทุกคืน (ปีงบปัจจุบัน + ปีงบก่อนในช่วง ต.ค.–ธ.ค.)</summary>
        public bool AutoRefreshEnabled { get; set; } = true;

        /// <summary>ชั่วโมงที่ประมวลผลอัตโนมัติ (เวลาไทย 0–23)</summary>
        public int AutoRefreshHour { get; set; } = 2;

        public const string TYPE_SOURCE_VISIT = "VISIT";
        public const string TYPE_SOURCE_BILL = "BILL";

        public FiscalSchemeOptions? Scheme(string? code)
            => EffectiveSchemes.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// สิทธิ์ของแถว (null = ไม่อยู่ในรายงาน): 1) คำในชื่อผู้จ่าย/ชื่อเงื่อนไข (PayorNameKeywords) 2) กลุ่มผู้จ่าย (PYRACAT)
        /// </summary>
        public string? SchemeOf(string? payorCategoryCode, string? payorName = null, string? agreementName = null)
        {
            static bool Has(string? text, string keyword) => !string.IsNullOrWhiteSpace(text) && text.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);

            var byName = EffectiveSchemes.FirstOrDefault(s => s.PayorNameKeywords
                .Any(k => !string.IsNullOrWhiteSpace(k) && (Has(payorName, k) || Has(agreementName, k))));
            return byName?.Code
                ?? EffectiveSchemes.FirstOrDefault(s => s.PayorCategoryCodes.Contains(payorCategoryCode ?? "", StringComparer.OrdinalIgnoreCase))?.Code;
        }

        /// <summary>คำในชื่อผู้จ่าย/เงื่อนไขทั้งหมดที่ต้องดึงจาก HIS เพิ่ม (นอกเหนือจากกลุ่มผู้จ่าย)</summary>
        public IReadOnlyList<string> AllPayorNameKeywords
            => EffectiveSchemes.SelectMany(s => s.PayorNameKeywords).Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>กลุ่มผู้จ่ายทั้งหมดที่ต้องดึงจาก HIS</summary>
        public IReadOnlyList<string> AllPayorCategoryCodes
            => EffectiveSchemes.SelectMany(s => s.PayorCategoryCodes).Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public bool IsOutOfZone(string? agreementName)
        {
            if (string.IsNullOrWhiteSpace(agreementName)) return false;

            var ignore = OutOfZoneIgnorePhrases.Length > 0 ? OutOfZoneIgnorePhrases : DEFAULT_OUT_OF_ZONE_IGNORE;
            var keywords = OutOfZoneKeywords.Length > 0 ? OutOfZoneKeywords : DEFAULT_OUT_OF_ZONE_KEYWORDS;
            var name = ignore
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Aggregate(agreementName, (current, pattern) =>
                    Regex.Replace(current, pattern.Trim(), " ", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)));

            return keywords.Any(k => !string.IsNullOrWhiteSpace(k) && name.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>ประเภทผู้ป่วย: ประเภท visit (VN/AN) ก่อน — ไม่พบจึงใช้ประเภทบิลใน HIS</summary>
        public (string Type, string Source) ResolvePatientType(string billSide, string? visitEncounterCode)
        {
            if (!string.IsNullOrWhiteSpace(visitEncounterCode)
                && VisitEncounterTypes.TryGetValue(visitEncounterCode.Trim(), out var mapped)
                && mapped is PatientTypeCodes.IPD or PatientTypeCodes.OPD)
            {
                return (mapped, TYPE_SOURCE_VISIT);
            }
            return (PatientTypeCodes.Normalize(billSide, PatientTypeCodes.OPD), TYPE_SOURCE_BILL);
        }
    }

    /// <summary>
    /// แถวจาก HIS ระดับผู้ป่วย (ใช้ในหน่วยความจำระหว่างประมวลผลเท่านั้น — ไม่บันทึก PatientUid)
    /// </summary>
    /// <param name="BillSide">ประเภทบิลใน HIS: O / I</param>
    /// <param name="VisitEncounterCode">ประเภท visit: OUPAT (VN) / INPAT (AN) / AEPAT / null</param>
    /// <param name="PayorName">ชื่อผู้จ่าย (PayorDetail.PayorName) เช่น "สปสช(อปท)" — ใช้จัดสิทธิ์ตาม PayorNameKeywords</param>
    public sealed record FiscalPatientRow(string BillSide, string? VisitEncounterCode, string PayorCategoryCode, string AgreementName,
        long PatientUid, int Visits, int Bills, decimal NetAmount, string? PayorName = null);

    /// <summary>ผลประมวลผล 1 ปีงบ (ทุกสิทธิ์)</summary>
    public sealed record FiscalYearData(IReadOnlyList<FiscalPayorSummary> Agreements, IReadOnlyList<FiscalSchemeTotal> Totals, int SourceRows);

    /// <summary>
    /// รวมแถวระดับผู้ป่วยเป็นผลสรุป — นับผู้ป่วยแบบไม่ซ้ำ (คน) ต่อ สิทธิ์ × ประเภทผู้ป่วย × เขต และต่อเงื่อนไขการเรียกเก็บ
    /// แยกออกมาเพื่อทดสอบได้โดยไม่ต้องต่อฐานข้อมูล
    /// </summary>
    public sealed class FiscalYearAggregator
    {
        private sealed class Bucket
        {
            public readonly HashSet<long> Patients = [];
            public int Visits, Bills;
            public decimal Amount;

            public void Add(FiscalPatientRow r)
            {
                Patients.Add(r.PatientUid);
                Visits += r.Visits;
                Bills += r.Bills;
                Amount += r.NetAmount;
            }
        }

        private readonly int _fiscalYear;
        private readonly FiscalReportOptions _options;
        private readonly Dictionary<(string Scheme, string Type, string Zone), Bucket> _totals = [];
        private readonly Dictionary<(string Scheme, string Type, string? Encounter, bool Moved, string Category, string Agreement), Bucket> _agreements = [];
        private readonly Dictionary<string, bool> _zoneCache = new(StringComparer.Ordinal);

        public FiscalYearAggregator(int fiscalYear, FiscalReportOptions options)
        {
            _fiscalYear = fiscalYear;
            _options = options;
        }

        public int Rows { get; private set; }

        public void Add(FiscalPatientRow row)
        {
            Rows++;
            var scheme = _options.SchemeOf(row.PayorCategoryCode, row.PayorName, row.AgreementName);
            if (scheme is null) return;

            var (type, _) = _options.ResolvePatientType(row.BillSide, row.VisitEncounterCode);
            if (!_zoneCache.TryGetValue(row.AgreementName, out var outOfZone))
            {
                outOfZone = _options.IsOutOfZone(row.AgreementName);
                _zoneCache[row.AgreementName] = outOfZone;
            }

            Get(_totals, (scheme, type, outOfZone ? FiscalZoneCodes.OUT : FiscalZoneCodes.IN)).Add(row);
            Get(_totals, (scheme, type, FiscalZoneCodes.ALL)).Add(row);
            var moved = PatientTypeCodes.Normalize(row.BillSide, PatientTypeCodes.OPD) != type;
            Get(_agreements, (scheme, type, row.VisitEncounterCode, moved, row.PayorCategoryCode, row.AgreementName)).Add(row);
        }

        public FiscalYearData Result(DateTime refreshedAt)
        {
            var totals = _totals.Select(kv => new FiscalSchemeTotal
            {
                FiscalYear = _fiscalYear,
                SchemeCode = kv.Key.Scheme,
                PatientTypeCode = kv.Key.Type,
                ZoneCode = kv.Key.Zone,
                Patients = kv.Value.Patients.Count,
                Visits = kv.Value.Visits,
                NetAmount = kv.Value.Amount,
                RefreshedAt = refreshedAt,
            }).ToList();

            var agreements = _agreements.Select(kv => new FiscalPayorSummary
            {
                FiscalYear = _fiscalYear,
                SchemeCode = kv.Key.Scheme,
                PatientTypeCode = kv.Key.Type,
                VisitEncounterCode = kv.Key.Encounter,
                MovedFromBillSide = kv.Key.Moved,
                PayorCategoryCode = kv.Key.Category,
                AgreementName = kv.Key.Agreement.Length > 400 ? kv.Key.Agreement[..400] : kv.Key.Agreement,
                Patients = kv.Value.Patients.Count,
                Visits = kv.Value.Visits,
                Bills = kv.Value.Bills,
                NetAmount = kv.Value.Amount,
                RefreshedAt = refreshedAt,
            }).ToList();

            return new FiscalYearData(agreements, totals, Rows);
        }

        private static Bucket Get<TKey>(Dictionary<TKey, Bucket> map, TKey key) where TKey : notnull
        {
            if (!map.TryGetValue(key, out var bucket)) map[key] = bucket = new Bucket();
            return bucket;
        }
    }

    /// <param name="Patients">จำนวนผู้ป่วย (คน ไม่ซ้ำ)</param>
    public sealed record ZoneFigure(int Patients, int Visits, decimal Amount, decimal Percent);

    public sealed record FiscalYearRow(string Label, ZoneFigure InZone, ZoneFigure OutZone, ZoneFigure Total);

    /// <summary>เงื่อนไขการเรียกเก็บที่พบ พร้อมการจัดกลุ่ม — ให้ผู้ใช้ตรวจว่าจัดในเขต/นอกเขตถูกต้อง</summary>
    /// <param name="MovedFromBillSide">ประเภทบิลใน HIS ไม่ตรงกับประเภท visit (VN/AN)</param>
    public sealed record AgreementSummary(string PayorCategoryCode, string AgreementName, bool OutOfZone, int Patients, int Visits, decimal Amount,
        bool MovedFromBillSide = false, string? EncounterCode = null);

    public sealed class FiscalReport
    {
        public required string SchemeCode { get; init; }
        public required string SchemeName { get; init; }
        public required string Side { get; init; }
        public required IReadOnlyList<FiscalYear> Years { get; init; }
        public required IReadOnlyList<FiscalYearRow> Rows { get; init; }
        public required FiscalYearRow Sum { get; init; }
        public required IReadOnlyList<AgreementSummary> Agreements { get; init; }
        public IReadOnlyList<string> PayorCategoryCodes { get; init; } = [];
        public IReadOnlyList<string> PayorNameKeywords { get; init; } = [];

        /// <summary>คำในชื่อผู้จ่ายที่ย้ายไปนับในสิทธิ์อื่น (เช่น "อปท" ไม่นับใน สปสช.) — แสดงเป็นหมายเหตุ</summary>
        public IReadOnlyList<string> ExcludedPayorNameKeywords { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public DateTime GeneratedAt { get; init; }

        /// <summary>เวลาที่ประมวลผลผลสรุปล่าสุด (เก่าที่สุดของปีที่เลือก)</summary>
        public DateTime? RefreshedAt { get; init; }

        public string SideName => Side == PatientTypeCodes.IPD ? "ผู้ป่วยใน (IPD)" : "ผู้ป่วยนอก (OPD)";

        /// <summary>แยกในเขต/นอกเขต (เฉพาะ สปสช.) — false = แสดงเฉพาะกลุ่ม "รวม"</summary>
        public bool HasZones { get; init; } = true;

        /// <summary>ปีงบที่มีข้อมูลใน QSHC_Financial (ตัวเลือกปีในหน้ารายงาน)</summary>
        public IReadOnlyList<int> AvailableYears { get; init; } = [];

        /// <summary>หัวกลุ่ม — สปสช. = ในเขต/นอกเขต/รวม · สิทธิ์อื่นไม่มีหัวกลุ่ม (คอลัมน์เดียว)</summary>
        public IReadOnlyList<string> GroupNames => HasZones ? ["ในเขต", "นอกเขต", "รวม"] : [""];

        public IReadOnlyList<ZoneFigure> Figures(FiscalYearRow row) => HasZones ? [row.InZone, row.OutZone, row.Total] : [row.Total];
    }

    /// <summary>
    /// สร้างตารางรายงานจากผลสรุป
    /// ร้อยละ: ในเขต/นอกเขต = สัดส่วนจำนวนเงินของปีนั้น (ในเขต + นอกเขต = 100), รวม = สัดส่วนจำนวนเงินของปีนั้นต่อทุกปี
    /// จำนวน (ราย) = ผู้ป่วยไม่ซ้ำในปีนั้น · แถวรวมหลายปี = ผลรวมรายปี
    /// </summary>
    public static class FiscalReportBuilder
    {
        public static FiscalReport Build(FiscalSchemeOptions scheme, string schemeName, string side, IReadOnlyList<FiscalYear> years,
            IReadOnlyList<FiscalSchemeTotal> totals, IReadOnlyList<FiscalPayorSummary> agreements, FiscalReportOptions options,
            DateTime generatedAt, IReadOnlyList<string>? warnings = null, DateTime? refreshedAt = null, IReadOnlyList<int>? availableYears = null)
        {
            var mine = totals.Where(t => t.SchemeCode == scheme.Code && t.PatientTypeCode == side).ToList();
            var grandAmount = mine.Where(t => t.ZoneCode == FiscalZoneCodes.ALL).Sum(t => t.NetAmount);

            FiscalYearRow Row(string label, IReadOnlyCollection<FiscalSchemeTotal> source)
            {
                (int Patients, int Visits, decimal Amount) Zone(string zone)
                {
                    var rows = source.Where(t => t.ZoneCode == zone).ToList();
                    return (rows.Sum(t => t.Patients), rows.Sum(t => t.Visits), rows.Sum(t => t.NetAmount));
                }
                var (inZ, outZ, all) = (Zone(FiscalZoneCodes.IN), Zone(FiscalZoneCodes.OUT), Zone(FiscalZoneCodes.ALL));
                var yearAmount = inZ.Amount + outZ.Amount;

                return new FiscalYearRow(
                    label,
                    new ZoneFigure(inZ.Patients, inZ.Visits, inZ.Amount, Percent(inZ.Amount, yearAmount)),
                    new ZoneFigure(outZ.Patients, outZ.Visits, outZ.Amount, Percent(outZ.Amount, yearAmount)),
                    new ZoneFigure(all.Patients, all.Visits, all.Amount, Percent(all.Amount, grandAmount)));
            }

            var yearRows = years.Select(y => Row(y.Label, mine.Where(t => t.FiscalYear == y.BuddhistYear).ToList())).ToList();

            var agreementRows = agreements
                .Where(a => a.SchemeCode == scheme.Code && a.PatientTypeCode == side)
                .GroupBy(a => (a.PayorCategoryCode, a.AgreementName, a.MovedFromBillSide, a.VisitEncounterCode))
                .Select(g => new AgreementSummary(g.Key.PayorCategoryCode, g.Key.AgreementName, options.IsOutOfZone(g.Key.AgreementName),
                    g.Sum(a => a.Patients), g.Sum(a => a.Visits), g.Sum(a => a.NetAmount), g.Key.MovedFromBillSide, g.Key.VisitEncounterCode))
                .OrderByDescending(a => a.Amount)
                .ToList();

            return new FiscalReport
            {
                SchemeCode = scheme.Code,
                SchemeName = schemeName,
                Side = side,
                Years = years,
                Rows = yearRows,
                Sum = Row("รวม", mine),
                Agreements = agreementRows,
                PayorCategoryCodes = scheme.PayorCategoryCodes,
                PayorNameKeywords = scheme.PayorNameKeywords,
                ExcludedPayorNameKeywords = options.EffectiveSchemes.Where(s => s.Code != scheme.Code).SelectMany(s => s.PayorNameKeywords).Distinct().ToList(),
                Warnings = warnings ?? [],
                GeneratedAt = generatedAt,
                RefreshedAt = refreshedAt,
                HasZones = scheme.HasZones,
                AvailableYears = availableYears ?? [],
            };
        }

        private static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole * 100, 2);
    }
}
