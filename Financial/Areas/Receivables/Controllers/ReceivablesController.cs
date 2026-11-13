using Financial.Models;
using Financial.Navigation;
using Financial.Services.Authorization;
using Financial.Services.Receivables;
using Financial.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Financial.Areas.Receivables.Controllers
{
    /// <summary>
    /// รายการลูกหนี้สิทธิ — ค้น/กรอง/ส่งออก Excel
    /// PHI: HN และ VN/AN เต็มแสดงเฉพาะ Admin และบันทึก audit ทุกครั้ง · ผู้อื่นเห็นเลขบิล + VN/AN ปิดบัง
    /// </summary>
    [Area(ReceivablesMenu.AREA)]
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class ReceivablesController : Controller
    {
        private readonly IReceivableQueryService _query;
        private readonly FiscalReportOptions _fiscal;
        private readonly IOptions<RoleOptions> _roles;
        private readonly ILogger<ReceivablesController> _logger;

        public ReceivablesController(IReceivableQueryService query, IOptions<FiscalReportOptions> fiscal, IOptions<RoleOptions> roles, ILogger<ReceivablesController> logger)
        {
            _query = query;
            _fiscal = fiscal.Value;
            _roles = roles;
            _logger = logger;
        }

        private bool IsAdmin => AppPolicies.IsAdmin(User, _roles.Value);

        // GET: /Receivables/Receivables?scheme=NHSO&side=I&status=&aging=91-180&payor=&billFrom=&billTo=&search=&page=1
        public async Task<IActionResult> Index(string? scheme, string? side, string? status, string? aging, string? payor,
            DateTime? billFrom, DateTime? billTo, string? search, int page, bool review, CancellationToken cancellationToken)
        {
            var query = Normalize(scheme, side, status, aging, payor, billFrom, billTo, search, page, review);
            var result = await _query.GetListAsync(query, IsAdmin, cancellationToken: cancellationToken);
            Audit(result.Items, "list");

            ViewData["Query"] = query;
            ViewData["StatusNames"] = await _query.StatusNamesAsync();
            ViewData["HasZones"] = _fiscal.Scheme(query.SchemeCode)?.HasZones ?? false;
            ViewData["ShowIdentity"] = IsAdmin;
            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> Export(string? scheme, string? side, string? status, string? aging, string? payor,
            DateTime? billFrom, DateTime? billTo, string? search, bool review, CancellationToken cancellationToken)
        {
            var query = Normalize(scheme, side, status, aging, payor, billFrom, billTo, search, 1, review);
            var admin = IsAdmin;
            var result = await _query.GetListAsync(query, admin, ReceivableQueryService.EXPORT_MAX_ROWS, cancellationToken);
            Audit(result.Items, "excel export");

            var content = ReceivableExcel.Create(result, query, await _query.StatusNamesAsync(), admin, _fiscal.Scheme(query.SchemeCode)?.HasZones ?? false);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AR_{query.SchemeCode}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>ตรวจแล้ว: ล้างธง "ต้องตรวจ" (บิลใน HIS เปลี่ยนหลังส่งเรียกเก็บ) พร้อมบันทึกหมายเหตุในประวัติ</summary>
        [HttpPost]
        public async Task<IActionResult> ClearReview(int id, string? note, string? scheme, CancellationToken cancellationToken)
        {
            var ok = await _query.ClearReviewAsync(id, note, User.Identity?.Name, cancellationToken);
            TempData[ok ? "Message" : "Error"] = ok ? "บันทึกว่าตรวจแล้ว" : "ไม่พบรายการที่ต้องตรวจ";
            return RedirectToAction(nameof(Index), new { scheme, review = true });
        }

        internal ReceivableListQuery Normalize(string? scheme, string? side, string? status, string? aging, string? payor,
            DateTime? billFrom, DateTime? billTo, string? search, int page, bool review = false)
        {
            var schemeCode = _fiscal.Scheme(scheme)?.Code ?? _fiscal.EffectiveSchemes[0].Code;
            var s = side?.Trim().ToUpperInvariant() is "O" or "I" ? side.Trim().ToUpperInvariant() : null;
            var st = status == "ALL" || ArStatusCodes.ALL.Contains(status ?? "") ? status : null;
            if (billFrom is not null && billTo is not null && billFrom > billTo) (billFrom, billTo) = (billTo, billFrom);
            return new ReceivableListQuery(schemeCode, s, st, AgingRanges.Find(aging)?.Code, payor?.Trim(), billFrom, billTo, search?.Trim(), Math.Max(1, page), review);
        }

        private void Audit(IReadOnlyList<ReceivableListItem> items, string action)
        {
            if (IsAdmin && items.Any(i => i.Hn is not null))
            {
                _logger.LogInformation("AUDIT: Admin {User} viewed patient identity (HN/VN) on receivables {Action} from {Ip}, {Count} rows",
                    User.Identity?.Name, action, HttpContext.Connection.RemoteIpAddress, items.Count);
            }
        }
    }

    /// <summary>ส่งออกรายการลูกหนี้ — คอลัมน์ HN มีเฉพาะ Admin</summary>
    public static class ReceivableExcel
    {
        static ReceivableExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public static byte[] Create(ReceivableListResult result, ReceivableListQuery query, IReadOnlyDictionary<string, string> statusNames, bool includeHn, bool hasZones)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add($"ลูกหนี้ {query.SchemeCode}");

            var headers = new List<string> { "เลขที่บิล", "VN/AN" };
            if (includeHn) headers.Add("HN");
            headers.AddRange(["ประเภท", "วันที่เปิด visit", "วันที่จำหน่าย", "วันออกบิล", "อายุหนี้ (วัน)", "ผู้จ่าย", "เงื่อนไขการเรียกเก็บ"]);
            if (hasZones) headers.Add("เขต");
            headers.AddRange(["ยอดเรียกเก็บ", "ได้รับแล้ว", "คงค้าง", "สถานะ"]);

            for (var c = 0; c < headers.Count; c++) ws.Cells[1, c + 1].Value = headers[c];
            var header = ws.Cells[1, 1, 1, headers.Count];
            header.Style.Font.Bold = true;
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(221, 235, 247));

            var row = 2;
            foreach (var i in result.Items)
            {
                var col = 1;
                ws.Cells[row, col++].Value = i.BillNumber;
                ws.Cells[row, col++].Value = i.VisitNumber;
                if (includeHn) ws.Cells[row, col++].Value = i.Hn;
                ws.Cells[row, col++].Value = PatientTypeCodes.Abbreviation(i.PatientTypeCode);
                Date(ws.Cells[row, col++], i.VisitStartDate);
                Date(ws.Cells[row, col++], i.DischargeDate);
                Date(ws.Cells[row, col++], i.BillDate);
                ws.Cells[row, col++].Value = i.AgeDays;
                ws.Cells[row, col++].Value = i.PayorName;
                ws.Cells[row, col++].Value = i.AgreementName;
                if (hasZones) ws.Cells[row, col++].Value = i.IsOutOfZone ? "นอกเขต" : "ในเขต";
                foreach (var amount in new[] { i.BilledAmount, i.PaidAmount, i.OutstandingAmount })
                {
                    ws.Cells[row, col].Value = amount;
                    ws.Cells[row, col++].Style.Numberformat.Format = "#,##0.00";
                }
                ws.Cells[row, col].Value = statusNames.GetValueOrDefault(i.StatusCode, i.StatusCode);
                row++;
            }

            ws.Cells[row, 1].Value = "รวมคงค้าง";
            ws.Cells[row, headers.Count - 1].Value = result.TotalOutstanding;
            ws.Cells[row, headers.Count - 1].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[row, 1, row, headers.Count].Style.Font.Bold = true;
            if (result.Total > result.Items.Count)
            {
                ws.Cells[row + 1, 1].Value = $"หมายเหตุ: ส่งออก {result.Items.Count:N0} จาก {result.Total:N0} รายการ — จำกัดช่วงข้อมูลให้แคบลง";
            }

            ws.Cells[1, 1, row, headers.Count].AutoFitColumns();
            return package.GetAsByteArray();
        }

        private static void Date(ExcelRange cell, DateTime? value)
        {
            if (value is null) return;
            cell.Value = value.Value;
            cell.Style.Numberformat.Format = "dd/mm/yyyy";
        }
    }
}
