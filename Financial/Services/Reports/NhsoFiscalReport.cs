using Financial.Models;
using System.Globalization;

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

    /// <summary>
    /// การตั้งค่ารายงาน (appsettings.json → Reports:Nhso)
    /// </summary>
    public class NhsoReportOptions
    {
        public const string SECTION_NAME = "Reports:Nhso";

        /// <summary>กลุ่มผู้จ่าย (PYRACAT) ที่ถือเป็น สปสช.: A สปสช, E สปสช.(เหมาจ่าย), M สปสช(เฉพาะ)</summary>
        public string[] PayorCategoryCodes { get; set; } = ["A"];

        /// <summary>เงื่อนไขการเรียกเก็บ (PayorAgreement.Name) ที่มีคำเหล่านี้ = นอกเขต (นอกจังหวัด); มีคำว่า "ใน" หรือไม่มีทั้งสองคำ = ในเขต</summary>
        public string[] OutOfZoneKeywords { get; set; } = ["นอก"];

        public int CommandTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// "Summary" = อ่านผลที่ประมวลผลไว้แล้วจากตาราง NhsoFiscalSummaries ใน QSHC_Financial (เร็ว — ค่าเริ่มต้น)
        /// "Query" = คำนวณจากตารางบิลใน HIS โดยตรงทุกครั้ง (ช้า ~นาที/ปีงบ)
        /// </summary>
        public string Source { get; set; } = SOURCE_SUMMARY;

        public const string SOURCE_SUMMARY = "Summary";
        public const string SOURCE_QUERY = "Query";

        public bool UseSummary => !string.Equals(Source, SOURCE_QUERY, StringComparison.OrdinalIgnoreCase);

        /// <summary>ประมวลผลอัตโนมัติทุกคืน (ปีงบปัจจุบัน + ปีงบก่อนในช่วง ต.ค.–ธ.ค.)</summary>
        public bool AutoRefreshEnabled { get; set; } = true;

        /// <summary>ชั่วโมงที่ประมวลผลอัตโนมัติ (เวลาไทย 0–23)</summary>
        public int AutoRefreshHour { get; set; } = 2;

        /// <summary>วลีที่มีคำว่า "นอก" แต่ไม่ได้หมายถึงนอกเขต — ตัดออกก่อนตรวจ OutOfZoneKeywords</summary>
        /// <remarks>เป็น Regular Expression — ค่าเริ่มต้นข้าม "ผู้ป่วยนอก" ที่ไม่ได้ตามด้วย เขต/จังหวัด/ขอนแก่น และ "นอกเวลา"</remarks>
        public string[] OutOfZoneIgnorePhrases { get; set; } = ["ผู้ป่วยนอก(?!เขต|จังหวัด|ขอนแก่น)", "นอกเวลา"];

        /// <summary>
        /// true = ถ้าชื่อเงื่อนไขการเรียกเก็บระบุ IPD/OPD ให้ใช้ตามชื่อ (เช่น "ท.74 IPD" ที่เรียกเก็บผ่านบิล OPD นับเป็น IPD)
        /// false = ใช้ประเภทบิลใน HIS อย่างเดียว
        /// </summary>
        public bool PatientTypeFromAgreementName { get; set; } = true;

        public string[] IpdAgreementKeywords { get; set; } = ["IPD"];
        public string[] OpdAgreementKeywords { get; set; } = ["OPD"];

        /// <summary>
        /// จับคู่ประเภทผู้ป่วยของสิทธิ์ (ReferenceValue โดเมน ENTYP ใน HIS) → O/I
        /// OUPAT = OPD, INPAT = IPD, AEPAT (ER) = OPD — รหัสที่ไม่อยู่ในรายการ ถือว่าสิทธิ์ไม่ได้ระบุ
        /// </summary>
        public Dictionary<string, string> AgreementEncounterTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OUPAT"] = PatientTypeCodes.OPD,
            ["INPAT"] = PatientTypeCodes.IPD,
            ["AEPAT"] = PatientTypeCodes.OPD,
        };

        public bool IsOutOfZone(string? agreementName)
        {
            if (string.IsNullOrWhiteSpace(agreementName)) return false;

            var name = OutOfZoneIgnorePhrases
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Aggregate(agreementName, (current, pattern) =>
                    System.Text.RegularExpressions.Regex.Replace(current, pattern.Trim(), " ",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)));

            return OutOfZoneKeywords.Any(k => !string.IsNullOrWhiteSpace(k) && name.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public const string TYPE_SOURCE_AGREEMENT = "AGREEMENT";
        public const string TYPE_SOURCE_NAME = "NAME";
        public const string TYPE_SOURCE_BILL = "BILL";

        /// <summary>
        /// ประเภทผู้ป่วยที่ใช้ในรายงาน ตามลำดับ:
        /// 1) ประเภทของสิทธิ์ (รหัส ENTYP → AgreementEncounterTypes) 2) คำว่า IPD/OPD ในชื่อเงื่อนไข (ระบุอย่างใดอย่างหนึ่ง) 3) ประเภทบิลใน HIS
        /// </summary>
        public (string Type, string Source) ResolvePatientType(string billSide, string? agreementName, string? agreementEncounterCode = null)
        {
            if (!PatientTypeFromAgreementName) return (billSide, TYPE_SOURCE_BILL);

            if (!string.IsNullOrWhiteSpace(agreementEncounterCode)
                && AgreementEncounterTypes.TryGetValue(agreementEncounterCode.Trim(), out var mapped)
                && mapped is PatientTypeCodes.IPD or PatientTypeCodes.OPD)
            {
                return (mapped, TYPE_SOURCE_AGREEMENT);
            }

            if (!string.IsNullOrWhiteSpace(agreementName))
            {
                static bool Has(string text, IEnumerable<string> keywords)
                    => keywords.Any(k => !string.IsNullOrWhiteSpace(k) && text.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase));

                bool ipd = Has(agreementName, IpdAgreementKeywords), opd = Has(agreementName, OpdAgreementKeywords);
                if (ipd != opd) return (ipd ? PatientTypeCodes.IPD : PatientTypeCodes.OPD, TYPE_SOURCE_NAME);
            }

            return (billSide, TYPE_SOURCE_BILL);
        }

        public bool IsNhso(string? payorCategoryCode)
            => PayorCategoryCodes.Contains(payorCategoryCode ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>แถวจาก SQL (ต่อ ปีงบ × O/I × กลุ่มผู้จ่าย × เงื่อนไขการเรียกเก็บ)</summary>
    /// <param name="Side">ประเภทบิลใน HIS: O / I</param>
    /// <param name="AgreementEncounterCode">ประเภทผู้ป่วยของสิทธิ์ (PayorAgreement.ENTYPUID → ReferenceValue ENTYP): OUPAT / INPAT / AEPAT / null = ไม่ระบุ</param>
    public sealed record NhsoAgreementRow(int FiscalYear, string Side, string PayorCategoryCode, string AgreementName, int Visits, int Bills, decimal NetAmount, string? AgreementEncounterCode = null);

    public sealed record ZoneFigure(int Visits, decimal Amount, decimal Percent);

    public sealed record NhsoYearRow(string Label, ZoneFigure InZone, ZoneFigure OutZone, ZoneFigure Total);

    /// <summary>เงื่อนไขการเรียกเก็บที่พบ พร้อมการจัดกลุ่ม — ให้ผู้ใช้ตรวจว่าจัดในเขต/นอกเขตถูกต้อง</summary>
    /// <param name="MovedFromBillSide">ประเภทบิลใน HIS ไม่ตรงกับรายงานนี้ แต่ย้ายมาตามประเภทของสิทธิ์/ชื่อเงื่อนไข</param>
    /// <param name="TypeSource">ที่มาของประเภทผู้ป่วย: AGREEMENT (ประเภทของสิทธิ์), NAME (ชื่อเงื่อนไข), BILL (ประเภทบิล)</param>
    public sealed record AgreementSummary(string PayorCategoryCode, string AgreementName, bool OutOfZone, bool Counted, int Visits, decimal Amount,
        bool MovedFromBillSide = false, string TypeSource = NhsoReportOptions.TYPE_SOURCE_BILL, string? EncounterCode = null);

    public sealed class NhsoFiscalReport
    {
        public required string Side { get; init; }
        public required IReadOnlyList<FiscalYear> Years { get; init; }
        public required IReadOnlyList<NhsoYearRow> Rows { get; init; }
        public required NhsoYearRow Sum { get; init; }
        public required IReadOnlyList<AgreementSummary> Agreements { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public DateTime GeneratedAt { get; init; }

        /// <summary>เวลาที่ประมวลผลผลสรุปล่าสุด (เฉพาะโหมด Summary)</summary>
        public DateTime? RefreshedAt { get; init; }

        public string SideName => Side == "I" ? "ผู้ป่วยใน (IPD)" : "ผู้ป่วยนอก (OPD)";
    }

    /// <summary>
    /// รวมแถวจาก SQL เป็นตารางรายงาน — แยกออกมาเพื่อทดสอบได้โดยไม่ต้องต่อฐานข้อมูล
    /// ร้อยละ: ในเขต/นอกเขต = สัดส่วนจำนวนเงินของปีนั้น (ในเขต + นอกเขต = 100), รวม = สัดส่วนจำนวนเงินของปีนั้นต่อทุกปี
    /// </summary>
    public static class NhsoFiscalReportBuilder
    {
        public static NhsoFiscalReport Build(string side, IReadOnlyList<FiscalYear> years, IReadOnlyList<NhsoAgreementRow> rows,
            NhsoReportOptions options, DateTime generatedAt, IReadOnlyList<string>? warnings = null, DateTime? refreshedAt = null)
        {
            // ประเภทผู้ป่วย: ประเภทของสิทธิ์ → ชื่อเงื่อนไข → ประเภทบิล
            var resolved = rows.Select(r => (Row: r, Type: options.ResolvePatientType(r.Side, r.AgreementName, r.AgreementEncounterCode))).ToList();
            var sideRows = resolved.Where(x => x.Type.Type == side).Select(x => x.Row).ToList();
            var counted = sideRows.Where(r => options.IsNhso(r.PayorCategoryCode)).ToList();
            var grandAmount = counted.Sum(r => r.NetAmount);

            NhsoYearRow Row(string label, IReadOnlyCollection<NhsoAgreementRow> source, decimal totalBase)
            {
                var outRows = source.Where(r => options.IsOutOfZone(r.AgreementName)).ToList();
                var inRows = source.Except(outRows).ToList();
                decimal inAmount = inRows.Sum(r => r.NetAmount), outAmount = outRows.Sum(r => r.NetAmount), total = inAmount + outAmount;

                return new NhsoYearRow(
                    label,
                    new ZoneFigure(inRows.Sum(r => r.Visits), inAmount, Percent(inAmount, total)),
                    new ZoneFigure(outRows.Sum(r => r.Visits), outAmount, Percent(outAmount, total)),
                    new ZoneFigure(source.Sum(r => r.Visits), total, Percent(total, totalBase)));
            }

            var yearRows = years.Select(y => Row(y.Label, counted.Where(r => r.FiscalYear == y.BuddhistYear).ToList(), grandAmount)).ToList();

            var agreements = resolved
                .Where(x => x.Type.Type == side)
                .GroupBy(x => (x.Row.PayorCategoryCode, x.Row.AgreementName, Moved: x.Row.Side != side, x.Type.Source, x.Row.AgreementEncounterCode))
                .Select(g => new AgreementSummary(g.Key.PayorCategoryCode, g.Key.AgreementName, options.IsOutOfZone(g.Key.AgreementName),
                    options.IsNhso(g.Key.PayorCategoryCode), g.Sum(x => x.Row.Visits), g.Sum(x => x.Row.NetAmount), g.Key.Moved, g.Key.Source, g.Key.AgreementEncounterCode))
                .OrderByDescending(a => a.Counted).ThenByDescending(a => a.Amount)
                .ToList();

            return new NhsoFiscalReport
            {
                Side = side,
                Years = years,
                Rows = yearRows,
                Sum = Row("รวม", counted, grandAmount),
                Agreements = agreements,
                Warnings = warnings ?? [],
                GeneratedAt = generatedAt,
                RefreshedAt = refreshedAt,
            };
        }

        private static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole * 100, 2);
    }
}
