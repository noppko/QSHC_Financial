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
    /// การดึงลูกหนี้จาก HIS (เลือกวันที่เริ่ม–สิ้นสุด) อยู่ที่หน้า ตั้งค่าระบบ (AdminController.RefreshReceivables)
    /// </summary>
    [Area(ReceivablesMenu.AREA)]
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class HomeController : Controller
    {
        private readonly IReceivableQueryService _query;
        private readonly ReceivableSyncQueue _syncQueue;
        private readonly FiscalReportOptions _fiscal;

        public HomeController(IReceivableQueryService query, ReceivableSyncQueue syncQueue, IOptions<FiscalReportOptions> fiscal)
        {
            _query = query;
            _syncQueue = syncQueue;
            _fiscal = fiscal.Value;
        }

        // GET: /Receivables?scheme=NHSO
        public async Task<IActionResult> Index(string? scheme, CancellationToken cancellationToken)
        {
            ViewData["SyncStatus"] = _syncQueue.Status; // แสดงว่ากำลังดึงข้อมูลอยู่หรือไม่ (ปุ่มดึงอยู่หน้า ตั้งค่าระบบ)
            return View(await _query.GetOverviewAsync(_fiscal.Scheme(scheme)?.Code ?? _fiscal.EffectiveSchemes[0].Code, cancellationToken));
        }
    }
}
