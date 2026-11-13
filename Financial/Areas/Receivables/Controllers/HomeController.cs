using Financial.Navigation;
using Financial.Services.Authorization;
using Financial.Services.Receivables;
using Financial.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Financial.Areas.Receivables.Controllers
{
    /// <summary>
    /// ส่วนงานลูกหนี้สิทธิ — ภาพรวมแยก 4 สิทธิ์ (Admin และ role Cashier*)
    /// ดึงลูกหนี้จาก HIS (Admin) ทำงานเบื้องหลังผ่าน ReceivableSyncQueue
    /// </summary>
    [Area(ReceivablesMenu.AREA)]
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class HomeController : Controller
    {
        public static readonly int[] SYNC_DAY_OPTIONS = [30, 90, 180, 365, 730];

        private readonly IReceivableQueryService _query;
        private readonly ReceivableSyncQueue _syncQueue;
        private readonly FiscalReportOptions _fiscal;
        private readonly ReceivableOptions _options;

        public HomeController(IReceivableQueryService query, ReceivableSyncQueue syncQueue, IOptions<FiscalReportOptions> fiscal, IOptions<ReceivableOptions> options)
        {
            _query = query;
            _syncQueue = syncQueue;
            _fiscal = fiscal.Value;
            _options = options.Value;
        }

        // GET: /Receivables?scheme=NHSO
        public async Task<IActionResult> Index(string? scheme, CancellationToken cancellationToken)
        {
            ViewData["SyncDays"] = SYNC_DAY_OPTIONS.Where(d => d <= _options.MaxSyncDaysBack).ToArray();
            ViewData["DefaultSyncDays"] = _options.SyncDaysBack;
            ViewData["SyncStatus"] = _syncQueue.Status;
            return View(await _query.GetOverviewAsync(NormalizeScheme(scheme), cancellationToken));
        }

        /// <summary>สั่งดึงลูกหนี้จาก HIS ย้อนหลัง N วัน (ทำงานเบื้องหลัง)</summary>
        [HttpPost]
        [Authorize(Policy = AppPolicies.ADMIN)]
        public IActionResult Sync(int days)
        {
            days = Math.Clamp(days <= 0 ? _options.SyncDaysBack : days, 1, _options.MaxSyncDaysBack);
            var queued = _syncQueue.Enqueue(days);
            return Json(new { queued, message = queued ? $"เริ่มดึงลูกหนี้ย้อนหลัง {days} วัน" : "มีงานดึงข้อมูลกำลังทำอยู่", status = _syncQueue.Status });
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.ADMIN)]
        public IActionResult SyncStatus() => Json(_syncQueue.Status);

        private string NormalizeScheme(string? scheme) => _fiscal.Scheme(scheme)?.Code ?? _fiscal.EffectiveSchemes[0].Code;
    }
}
