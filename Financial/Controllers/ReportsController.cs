using Financial.Models;
using Financial.Services.Authorization;
using Financial.Services.Dashboard;
using Financial.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Financial.Controllers
{
    /// <summary>
    /// รายงานสิทธิ์ตามปีงบประมาณ (Admin และ role Cashier*): สปสช. / กรมบัญชีกลาง / ประกันสังคม
    /// ข้อมูลอ่านจากผลสรุปใน QSHC_Financial · การดึงข้อมูลจาก HIS (พร้อม progress bar) อยู่ที่หน้า ตั้งค่าระบบ (Admin)
    /// </summary>
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class ReportsController : Controller
    {
        /// <summary>ชื่อ action ของแต่ละสิทธิ์ (เมนูแยกกัน)</summary>
        public static readonly IReadOnlyDictionary<string, string> SCHEME_ACTIONS = new Dictionary<string, string>
        {
            [PayorSchemeCodes.NHSO] = nameof(NhsoFiscal),
            [PayorSchemeCodes.CSMBS] = nameof(CsmbsFiscal),
            [PayorSchemeCodes.SSO] = nameof(SsoFiscal),
            [PayorSchemeCodes.LGO] = nameof(LgoFiscal),
        };

        private readonly IFiscalReportService _reportService;
        private readonly FiscalReportOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IFiscalReportService reportService, IOptions<FiscalReportOptions> options,
            TimeProvider timeProvider, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        // GET: /Reports/NhsoFiscal?side=I&from=2567&to=2569
        public Task<IActionResult> NhsoFiscal(string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
            => Fiscal(PayorSchemeCodes.NHSO, side, from, to, showVisits, cancellationToken);

        // GET: /Reports/CsmbsFiscal — กรมบัญชีกลาง
        public Task<IActionResult> CsmbsFiscal(string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
            => Fiscal(PayorSchemeCodes.CSMBS, side, from, to, showVisits, cancellationToken);

        // GET: /Reports/LgoFiscal — อปท.
        public Task<IActionResult> LgoFiscal(string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
            => Fiscal(PayorSchemeCodes.LGO, side, from, to, showVisits, cancellationToken);

        // GET: /Reports/SsoFiscal — ประกันสังคม
        public Task<IActionResult> SsoFiscal(string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
            => Fiscal(PayorSchemeCodes.SSO, side, from, to, showVisits, cancellationToken);

        private async Task<IActionResult> Fiscal(string scheme, string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
        {
            if (_options.Scheme(scheme) is null) return NotFound();

            var available = await _reportService.GetAvailableYearsAsync(scheme, cancellationToken);
            var (s, years, error) = Normalize(side, from, to, available);
            ViewData["Error"] = error;
            ViewData["CurrentFiscalYear"] = FiscalYear.Of(ThaiClock.Now(_timeProvider)).BuddhistYear;
            ViewData["SchemeAction"] = SCHEME_ACTIONS[scheme];
            ViewData["ShowVisits"] = showVisits; // แสดงคอลัมน์ จำนวน (visit) ข้าง จำนวน (ราย)
            return View("Fiscal", await _reportService.GetAsync(scheme, s, years, cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> ExportFiscal(string? scheme, string? side, int? from, int? to, bool showVisits, CancellationToken cancellationToken)
        {
            var schemeOptions = _options.Scheme(scheme);
            if (schemeOptions is null) return NotFound();

            var available = await _reportService.GetAvailableYearsAsync(schemeOptions.Code, cancellationToken);
            var (s, years, _) = Normalize(side, from, to, available);
            var report = await _reportService.GetAsync(schemeOptions.Code, s, years, cancellationToken);

            var content = FiscalExcel.Create(report, showVisits);
            var fileName = $"{schemeOptions.Code}_{s}PD_FY{years[0].Label}-{years[^1].Label}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// ค่าเริ่มต้น: IPD, ทุกปีงบที่มีข้อมูล (ไม่จำกัดจำนวนปี) — แสดงเฉพาะปีที่มีข้อมูลในช่วงที่เลือก
        /// ยังไม่มีข้อมูลเลย = ปีงบล่าสุดที่ปิดแล้ว 1 ปี (รายงานจะแจ้งให้ดึงข้อมูล)
        /// </summary>
        internal (string Side, IReadOnlyList<FiscalYear> Years, string? Error) Normalize(string? side, int? from, int? to, IReadOnlyCollection<int>? available = null)
        {
            var s = PatientTypeCodes.Normalize(side);
            var years = (available ?? []).Distinct().Order().ToList();
            if (years.Count == 0)
            {
                var current = FiscalYear.Of(ThaiClock.Now(_timeProvider)).BuddhistYear;
                return (s, [new FiscalYear(current - 1)], null);
            }

            int start = from ?? years[0], end = to ?? years[^1];
            if (start > end) (start, end) = (end, start);
            var selected = years.Where(y => y >= start && y <= end).ToList();

            string? error = null;
            if (selected.Count == 0)
            {
                selected = years;
                error = $"ไม่มีข้อมูลปีงบ {start}–{end} — แสดงทุกปีที่มีข้อมูล";
            }
            return (s, selected.Select(y => new FiscalYear(y)).ToList(), error);
        }
    }

    /// <summary>
    /// Excel รูปแบบเดียวกับตารางรายงาน (ในเขต / นอกเขต / รวม × จำนวน, จำนวนเงิน, ร้อยละ)
    /// </summary>
    public static class FiscalExcel
    {
        static FiscalExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <param name="showVisits">true = เพิ่มคอลัมน์ จำนวน (visit) ถัดจาก จำนวน (ราย) ของแต่ละกลุ่ม</param>
        public static byte[] Create(FiscalReport report, bool showVisits = false)
        {
            var per = showVisits ? 4 : 3;       // จำนวนคอลัมน์ต่อกลุ่ม (ในเขต/นอกเขต/รวม — สิทธิ์อื่นนอกจาก สปสช. มีเฉพาะ รวม)
            var groups = report.GroupNames;
            var lastCol = 1 + groups.Count * per;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add($"{report.SchemeCode} {report.Side}PD");

            ws.Cells[1, 1, 1, lastCol].Merge = true;
            ws.Cells["A1"].Value = $"{report.SchemeName} {report.Side}PD — {report.SideName}";
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // หัวตาราง 2 ชั้น
            // สปสช. = หัว 2 ชั้น (กลุ่ม + คอลัมน์) · สิทธิ์อื่น = ชั้นเดียว ไม่มีหัวกลุ่ม
            var subRow = report.HasZones ? 3 : 2;
            if (report.HasZones) ws.Cells["A2:A3"].Merge = true;
            ws.Cells["A2"].Value = "ปีงบประมาณ";
            for (var g = 0; g < groups.Count; g++)
            {
                var col = 2 + g * per;
                if (report.HasZones)
                {
                    ws.Cells[2, col, 2, col + per - 1].Merge = true;
                    ws.Cells[2, col].Value = groups[g];
                }
                ws.Cells[subRow, col].Value = "จำนวน (ราย)";
                if (showVisits) ws.Cells[subRow, col + 1].Value = "จำนวน (visit)";
                ws.Cells[subRow, col + (showVisits ? 2 : 1)].Value = "จำนวนเงิน (บาท)";
                ws.Cells[subRow, col + (showVisits ? 3 : 2)].Value = "ร้อยละ";
            }

            var header = ws.Cells[2, 1, subRow, lastCol];
            header.Style.Font.Bold = true;
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            header.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(221, 235, 247));

            var row = subRow + 1;
            foreach (var r in report.Rows.Append(report.Sum))
            {
                ws.Cells[row, 1].Value = r.Label;
                var col = 2;
                foreach (var z in report.Figures(r))
                {
                    ws.Cells[row, col].Value = z.Patients;
                    ws.Cells[row, col].Style.Numberformat.Format = "#,##0";
                    if (showVisits)
                    {
                        ws.Cells[row, col + 1].Value = z.Visits;
                        ws.Cells[row, col + 1].Style.Numberformat.Format = "#,##0";
                    }
                    var amountCol = col + (showVisits ? 2 : 1);
                    ws.Cells[row, amountCol].Value = z.Amount;
                    ws.Cells[row, amountCol].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[row, amountCol + 1].Value = z.Percent;
                    ws.Cells[row, amountCol + 1].Style.Numberformat.Format = "0.00";
                    col += per;
                }
                row++;
            }
            ws.Cells[row - 1, 1, row - 1, lastCol].Style.Font.Bold = true;   // แถวรวม

            var table = ws.Cells[2, 1, row - 1, lastCol];
            table.Style.Border.Top.Style = table.Style.Border.Bottom.Style = table.Style.Border.Left.Style = table.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            ws.Cells[row + 1, 1].Value = $"หมายเหตุ: นับตามวันออกบิลในปีงบประมาณ (1 ต.ค. – 30 ก.ย.), สิทธิ์หลัก {report.SchemeName} (กลุ่มผู้จ่าย {string.Join(", ", report.PayorCategoryCodes)}{(report.PayorNameKeywords.Count > 0 ? " หรือชื่อผู้จ่ายมีคำว่า " + string.Join(", ", report.PayorNameKeywords) : "")}), จำนวน (ราย) = ผู้ป่วยไม่ซ้ำ (คน) ในปีนั้น, OPD/IPD ตามประเภท visit (VN/AN){(report.HasZones ? ", นอกเขต = เงื่อนไขการเรียกเก็บมีคำว่า “นอก”" : "")}";
            ws.Cells[row + 1, 1].Style.Font.Italic = true;

            ws.Cells[2, 1, row - 1, lastCol].AutoFitColumns();
            return package.GetAsByteArray();
        }
    }
}
