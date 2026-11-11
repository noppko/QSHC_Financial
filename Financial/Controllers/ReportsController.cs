using Financial.Models;
using Financial.Services.Authorization;
using Financial.Services.Dashboard;
using Financial.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Financial.Controllers
{
    /// <summary>
    /// รายงานด้านการเงิน (Admin และ role Cashier*)
    /// </summary>
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class ReportsController : Controller
    {
        public const int MAX_YEARS = 5;

        private readonly INhsoFiscalReportService _reportService;
        private readonly TimeProvider _timeProvider;

        public ReportsController(INhsoFiscalReportService reportService, TimeProvider timeProvider)
        {
            _reportService = reportService;
            _timeProvider = timeProvider;
        }

        // GET: /Reports/NhsoFiscal?side=I&from=2567&to=2569
        public async Task<IActionResult> NhsoFiscal(string? side, int? from, int? to, CancellationToken cancellationToken)
        {
            var (s, years, error) = Normalize(side, from, to);
            ViewData["Error"] = error;
            ViewData["CurrentFiscalYear"] = FiscalYear.Of(ThaiClock.Now(_timeProvider)).BuddhistYear;
            return View(await _reportService.GetAsync(s, years, cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> ExportNhsoFiscal(string? side, int? from, int? to, CancellationToken cancellationToken)
        {
            var (s, years, _) = Normalize(side, from, to);
            var report = await _reportService.GetAsync(s, years, cancellationToken);

            var content = NhsoFiscalExcel.Create(report);
            var fileName = $"NHSO_{s}PD_FY{years[0].Label}-{years[^1].Label}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// ค่าเริ่มต้น: IPD, 3 ปีงบล่าสุดที่ปิดแล้ว — จำกัดไม่เกิน 5 ปีต่อครั้ง (query หนัก)
        /// </summary>
        internal (string Side, IReadOnlyList<FiscalYear> Years, string? Error) Normalize(string? side, int? from, int? to)
        {
            var s = PatientTypeCodes.Normalize(side);
            var current = FiscalYear.Of(ThaiClock.Now(_timeProvider)).BuddhistYear;
            var end = Math.Clamp(to ?? current - 1, 2550, current);
            var start = Math.Clamp(from ?? end - 2, 2550, end);
            string? error = null;

            if (end - start + 1 > MAX_YEARS)
            {
                start = end - MAX_YEARS + 1;
                error = $"เลือกได้ไม่เกิน {MAX_YEARS} ปีงบประมาณต่อครั้ง — แสดงปีงบ {start}–{end}";
            }

            var years = Enumerable.Range(start, end - start + 1).Select(y => new FiscalYear(y)).ToList();
            return (s, years, error);
        }
    }

    /// <summary>
    /// Excel รูปแบบเดียวกับตารางรายงาน (ในเขต / นอกเขต / รวม × จำนวน, จำนวนเงิน, ร้อยละ)
    /// </summary>
    public static class NhsoFiscalExcel
    {
        static NhsoFiscalExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public static byte[] Create(NhsoFiscalReport report)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add($"สปสช {report.Side}PD");

            ws.Cells["A1:J1"].Merge = true;
            ws.Cells["A1"].Value = $"สปสช. {report.Side}PD";
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // หัวตาราง 2 ชั้น
            ws.Cells["A2:A3"].Merge = true; ws.Cells["A2"].Value = "ปีงบประมาณ";
            string[] groups = ["ในเขต", "นอกเขต", "รวม"];
            for (var g = 0; g < groups.Length; g++)
            {
                var col = 2 + g * 3;
                ws.Cells[2, col, 2, col + 2].Merge = true;
                ws.Cells[2, col].Value = groups[g];
                ws.Cells[3, col].Value = "จำนวน (ราย)";
                ws.Cells[3, col + 1].Value = "จำนวนเงิน (บาท)";
                ws.Cells[3, col + 2].Value = "ร้อยละ";
            }

            var header = ws.Cells["A2:J3"];
            header.Style.Font.Bold = true;
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            header.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(221, 235, 247));

            var row = 4;
            foreach (var r in report.Rows.Append(report.Sum))
            {
                ws.Cells[row, 1].Value = r.Label;
                var col = 2;
                foreach (var z in new[] { r.InZone, r.OutZone, r.Total })
                {
                    ws.Cells[row, col].Value = z.Visits;
                    ws.Cells[row, col].Style.Numberformat.Format = "#,##0";
                    ws.Cells[row, col + 1].Value = z.Amount;
                    ws.Cells[row, col + 1].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[row, col + 2].Value = z.Percent;
                    ws.Cells[row, col + 2].Style.Numberformat.Format = "0.00";
                    col += 3;
                }
                row++;
            }
            ws.Cells[row - 1, 1, row - 1, 10].Style.Font.Bold = true;

            var table = ws.Cells[2, 1, row - 1, 10];
            table.Style.Border.Top.Style = table.Style.Border.Bottom.Style = table.Style.Border.Left.Style = table.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            ws.Cells[row + 1, 1].Value = "หมายเหตุ: นับตามวันออกบิลในปีงบประมาณ (1 ต.ค. – 30 ก.ย.), สิทธิ์หลัก สปสช., นอกเขต = เงื่อนไขการเรียกเก็บมีคำว่า “นอก” (ไม่มีคำว่าใน/นอก ถือเป็นในเขต)";
            ws.Cells[row + 1, 1].Style.Font.Italic = true;

            ws.Cells[2, 1, row - 1, 10].AutoFitColumns();
            return package.GetAsByteArray();
        }
    }
}
