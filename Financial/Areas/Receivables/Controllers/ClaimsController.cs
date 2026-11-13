using Financial.Models;
using Financial.Navigation;
using Financial.Services;
using Financial.Services.Authorization;
using Financial.Services.Receivables;
using Financial.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Financial.Areas.Receivables.Controllers
{
    /// <summary>
    /// ชุดเรียกเก็บ — จัดชุด, ส่งออก Excel (template ผลการเรียกเก็บที่เติม VN/AN ไว้), บันทึกส่ง, ยกเลิก
    /// PHI: Excel ที่ส่งออกมี VN/AN เต็ม (จำเป็นต่อการเรียกเก็บ) ทุกครั้งบันทึก audit · HN ใส่เฉพาะเมื่อ Admin ส่งออก
    /// </summary>
    [Area(ReceivablesMenu.AREA)]
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class ClaimsController : Controller
    {
        private readonly ClaimBatchService _claims;
        private readonly IReferenceValueService _referenceValues;
        private readonly FiscalReportOptions _fiscal;
        private readonly IOptions<RoleOptions> _roles;
        private readonly ILogger<ClaimsController> _logger;

        public ClaimsController(ClaimBatchService claims, IReferenceValueService referenceValues, IOptions<FiscalReportOptions> fiscal,
            IOptions<RoleOptions> roles, ILogger<ClaimsController> logger)
        {
            _claims = claims;
            _referenceValues = referenceValues;
            _fiscal = fiscal.Value;
            _roles = roles;
            _logger = logger;
        }

        private bool IsAdmin => AppPolicies.IsAdmin(User, _roles.Value);

        private string Scheme(string? scheme) => _fiscal.Scheme(scheme)?.Code ?? _fiscal.EffectiveSchemes[0].Code;

        // GET: /Receivables/Claims?scheme=NHSO
        public async Task<IActionResult> Index(string? scheme, CancellationToken cancellationToken)
        {
            var code = Scheme(scheme);
            ViewData["Scheme"] = code;
            ViewData["SchemeName"] = await _referenceValues.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, code, _fiscal.Scheme(code)?.Name ?? code);
            return View(await _claims.GetBatchesAsync(code, cancellationToken: cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> Create(string? scheme, string? side, DateTime? billFrom, DateTime? billTo, string? payor, string? note, CancellationToken cancellationToken)
        {
            var code = Scheme(scheme);
            var s = side?.Trim().ToUpperInvariant() is PatientTypeCodes.OPD or PatientTypeCodes.IPD ? side.Trim().ToUpperInvariant() : null;
            if (billFrom is not null && billTo is not null && billFrom > billTo) (billFrom, billTo) = (billTo, billFrom);
            var result = await _claims.CreateAsync(new ClaimBatchCreate(code, s, billFrom, billTo, payor, note), cancellationToken);
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index), new { scheme = code });
            }
            TempData["Message"] = "จัดชุดแล้ว — ตรวจรายการ ส่งออก Excel แล้วกด \"บันทึกส่งแล้ว\" เมื่อส่งหน่วยงาน";
            return RedirectToAction(nameof(Detail), new { id = int.Parse(result.Message, System.Globalization.CultureInfo.InvariantCulture) });
        }

        // GET: /Receivables/Claims/Detail/5
        public async Task<IActionResult> Detail(int id, int page, CancellationToken cancellationToken)
        {
            var detail = await _claims.GetDetailAsync(id, page, cancellationToken);
            if (detail is null) return NotFound();
            ViewData["ShowIdentity"] = IsAdmin;
            ViewData["SchemeName"] = await _referenceValues.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, detail.Batch.SchemeCode, _fiscal.Scheme(detail.Batch.SchemeCode)?.Name ?? detail.Batch.SchemeCode);
            if (IsAdmin && detail.Items.Count > 0)
            {
                _logger.LogInformation("AUDIT: Admin {User} viewed patient identity (HN/VN) on claim batch {BatchNo} from {Ip}, {Count} rows",
                    User.Identity?.Name, detail.Batch.BatchNo, HttpContext.Connection.RemoteIpAddress, detail.Items.Count);
            }
            return View(detail);
        }

        /// <summary>Excel รายการเรียกเก็บ = template ผลการเรียกเก็บที่เติม VN/AN วันที่ เลขบิล ยอดเรียกเก็บไว้ — กรอกยอดที่ได้รับแล้วนำเข้าได้ทันที</summary>
        [HttpPost]
        public async Task<IActionResult> Export(int id, CancellationToken cancellationToken)
        {
            var admin = IsAdmin;
            var data = await _claims.ExportRowsAsync(id, admin, cancellationToken);
            if (data is null) return NotFound();
            var (batch, rows) = data.Value;
            _logger.LogInformation("AUDIT: {User} exported claim batch {BatchNo} ({Count} rows, VN/AN{Hn}) from {Ip}",
                User.Identity?.Name, batch.BatchNo, rows.Count, admin ? " + HN" : "", HttpContext.Connection.RemoteIpAddress);

            var schemeName = await _referenceValues.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, batch.SchemeCode, _fiscal.Scheme(batch.SchemeCode)?.Name ?? batch.SchemeCode);
            var reasons = await ReasonsAsync(batch.SchemeCode);
            var content = RemittanceExcel.CreateTemplate(batch.SchemeCode, schemeName, reasons, batch.BatchNo, rows);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CLAIM_{batch.BatchNo}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveItem(int id, int itemId, int page, CancellationToken cancellationToken)
            => Back(await _claims.RemoveItemAsync(id, itemId, cancellationToken), id, page);

        [HttpPost]
        public async Task<IActionResult> Send(int id, DateTime? sentDate, string? referenceDocNo, CancellationToken cancellationToken)
            => Back(await _claims.MarkSentAsync(id, sentDate, referenceDocNo, User.Identity?.Name, cancellationToken), id, 1);

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
            => Back(await _claims.CancelAsync(id, User.Identity?.Name, cancellationToken), id, 1);

        private RedirectToActionResult Back(ImportActionResult outcome, int id, int page)
        {
            TempData[outcome.Success ? "Message" : "Error"] = outcome.Message;
            return RedirectToAction(nameof(Detail), new { id, page = page > 1 ? page : (int?)null });
        }

        private async Task<IReadOnlyList<(string Code, string Name)>> ReasonsAsync(string scheme)
        {
            try
            {
                return (await _referenceValues.GetByCategoryAsync(ReferenceCategories.ArDenyReason(scheme)))
                    .OrderBy(v => v.DisplayOrder).Select(v => (v.Code, v.NameTH)).ToList();
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot read deny reasons for {Scheme}", scheme);
                return [];
            }
        }
    }
}
