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
    /// นำเข้าผลการเรียกเก็บ (Excel template แยกต่อสิทธิ์) → ตรวจผลจับคู่ → ยืนยันรับชำระ / ยกเลิก
    /// PHI: HN และ VN/AN เต็มในหน้าตรวจแสดงเฉพาะ Admin (audit) — ผู้อื่นเห็นเลขบิล + VN/AN ปิดบัง
    /// </summary>
    [Area(ReceivablesMenu.AREA)]
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class ImportsController : Controller
    {
        private readonly RemittanceImportService _imports;
        private readonly IReferenceValueService _referenceValues;
        private readonly FiscalReportOptions _fiscal;
        private readonly ReceivableOptions _options;
        private readonly IOptions<RoleOptions> _roles;
        private readonly ILogger<ImportsController> _logger;

        public ImportsController(RemittanceImportService imports, IReferenceValueService referenceValues, IOptions<FiscalReportOptions> fiscal,
            IOptions<ReceivableOptions> options, IOptions<RoleOptions> roles, ILogger<ImportsController> logger)
        {
            _imports = imports;
            _referenceValues = referenceValues;
            _fiscal = fiscal.Value;
            _options = options.Value;
            _roles = roles;
            _logger = logger;
        }

        private bool IsAdmin => AppPolicies.IsAdmin(User, _roles.Value);

        private string Scheme(string? scheme) => _fiscal.Scheme(scheme)?.Code ?? _fiscal.EffectiveSchemes[0].Code;

        // GET: /Receivables/Imports?scheme=NHSO
        public async Task<IActionResult> Index(string? scheme, CancellationToken cancellationToken)
        {
            var code = Scheme(scheme);
            ViewData["Scheme"] = code;
            ViewData["SchemeName"] = await SchemeNameAsync(code);
            ViewData["MaxFileMb"] = _options.MaxImportFileMb;
            ViewData["MaxRows"] = _options.MaxImportRows;
            return View(await _imports.GetBatchesAsync(code, cancellationToken: cancellationToken));
        }

        /// <summary>template เปล่าของสิทธิ์ (ไม่มีข้อมูลผู้ป่วย)</summary>
        [HttpGet]
        public async Task<IActionResult> Template(string? scheme)
        {
            var code = Scheme(scheme);
            var reasons = await ReasonsAsync(code);
            var content = RemittanceExcel.CreateTemplate(code, await SchemeNameAsync(code), reasons);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", RemittanceExcel.FileName(code));
        }

        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string? scheme, string? statementNo, DateTime? transferDate, IFormFile? file, CancellationToken cancellationToken)
        {
            var code = Scheme(scheme);
            string? error = null;
            if (file is null || file.Length == 0) error = "กรุณาเลือกไฟล์";
            else if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) error = "ไฟล์ต้องเป็น .xlsx (Excel) เท่านั้น";
            else if (file.Length > _options.MaxImportFileMb * 1024L * 1024L) error = $"ขนาดไฟล์เกิน {_options.MaxImportFileMb} MB";
            if (error is not null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index), new { scheme = code });
            }

            await using var stream = file!.OpenReadStream();
            var result = await _imports.CreateAsync(code, statementNo, transferDate, file.FileName, stream, cancellationToken);
            if (!result.Success)
            {
                TempData["Error"] = string.Join(" · ", result.Errors);
                return RedirectToAction(nameof(Index), new { scheme = code });
            }
            TempData["Message"] = "อ่านไฟล์และจับคู่แล้ว — ตรวจผลก่อนกดยืนยัน";
            return RedirectToAction(nameof(Review), new { id = result.BatchId });
        }

        // GET: /Receivables/Imports/Review/5?result=NOT_FOUND&page=1
        public async Task<IActionResult> Review(int id, string? result, int page, CancellationToken cancellationToken)
        {
            var review = await _imports.GetReviewAsync(id, ArMatchResultCodes.ALL.Contains(result ?? "") ? result : null, page, cancellationToken);
            if (review is null) return NotFound();

            var admin = IsAdmin;
            if (admin && review.Rows.Any(r => r.Hn is not null || r.Receivable?.Hn is not null))
            {
                _logger.LogInformation("AUDIT: Admin {User} viewed patient identity (HN/VN) on remittance import #{Id} from {Ip}, {Count} rows",
                    User.Identity?.Name, id, HttpContext.Connection.RemoteIpAddress, review.Rows.Count);
            }
            ViewData["ShowIdentity"] = admin;
            ViewData["Result"] = result;
            ViewData["SchemeName"] = await SchemeNameAsync(review.Batch.SchemeCode);
            ViewData["MatchNames"] = await NamesAsync(ReferenceCategories.AR_MATCH_RESULT, ArMatchResultCodes.DEFAULT_NAMES);
            ViewData["ImportStatusNames"] = await NamesAsync(ReferenceCategories.AR_IMPORT_STATUS, ArImportStatusCodes.DEFAULT_NAMES);
            return View(review);
        }

        [HttpPost]
        public async Task<IActionResult> Accept(int id, int rowId, bool accepted, string? result, int page, CancellationToken cancellationToken)
            => Back(await _imports.SetAcceptedAsync(rowId, accepted, cancellationToken), id, result, page);

        [HttpPost]
        public async Task<IActionResult> Match(int id, int rowId, string? billNumber, string? result, int page, CancellationToken cancellationToken)
            => Back(await _imports.ManualMatchAsync(rowId, billNumber, User.Identity?.Name, cancellationToken), id, result, page);

        [HttpPost]
        public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
            => Back(await _imports.ConfirmAsync(id, User.Identity?.Name, cancellationToken), id, null, 1);

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
            => Back(await _imports.CancelAsync(id, User.Identity?.Name, cancellationToken), id, null, 1);

        private RedirectToActionResult Back(ImportActionResult outcome, int id, string? result, int page)
        {
            TempData[outcome.Success ? "Message" : "Error"] = outcome.Message;
            return RedirectToAction(nameof(Review), new { id, result, page = page > 1 ? page : (int?)null });
        }

        private async Task<string> SchemeNameAsync(string code)
            => await _referenceValues.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, code, _fiscal.Scheme(code)?.Name ?? code);

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

        private async Task<Dictionary<string, string>> NamesAsync(string category, IReadOnlyDictionary<string, string> fallback)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var v in await _referenceValues.GetByCategoryAsync(category)) names.TryAdd(v.Code, v.NameTH);
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot read ReferenceValues {Category}", category);
            }
            foreach (var (code, name) in fallback) names.TryAdd(code, name);
            return names;
        }
    }
}
