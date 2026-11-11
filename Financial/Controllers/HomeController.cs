using Financial.Models;
using Financial.Models.Dashboard;
using Financial.Navigation;
using Financial.Services.Authorization;
using Financial.Services.Dashboard;
using Financial.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Financial.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFinanceDashboardService _dashboardService;
        private readonly IOptions<RoleOptions> _roleOptions;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IFinanceDashboardService dashboardService, IOptions<RoleOptions> roleOptions, ILogger<HomeController> logger)
        {
            _dashboardService = dashboardService;
            _roleOptions = roleOptions;
            _logger = logger;
        }

        /// <summary>
        /// Dashboard การเงิน — render ข้อมูลชุดแรกจาก server เพื่อให้หน้าแสดงผลทันที แล้ว refresh ผ่าน DashboardData
        /// ผู้ใช้ Admin จะเห็น HN และชื่อ-สกุลผู้ป่วยรอชำระ (ผู้ใช้อื่นไม่ได้รับข้อมูลนี้จาก server เลย)
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var dashboard = await LoadDashboardAsync(cancellationToken);
            ViewData["ShowPatientIdentity"] = IsAdmin;
            return View(dashboard);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> DashboardData(CancellationToken cancellationToken)
        {
            return Json(await LoadDashboardAsync(cancellationToken));
        }

        public async Task<IActionResult> SiteMap([FromServices] IAuthorizationService authorization, [FromServices] IAppSettingsStore settingsStore, CancellationToken cancellationToken)
            => View(await AppMenuFilter.VisibleGroupsAsync(authorization, User, settingsStore, cancellationToken));

        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private bool IsAdmin => AppPolicies.IsAdmin(User, _roleOptions.Value);

        /// <summary>
        /// HN/ชื่อ-สกุลเป็นข้อมูลส่วนบุคคล (PDPA): ขอเฉพาะเมื่อผู้ใช้เป็น Admin และบันทึกการเข้าถึงทุกครั้ง
        /// </summary>
        private async Task<FinanceDashboard> LoadDashboardAsync(CancellationToken cancellationToken)
        {
            var admin = IsAdmin;
            var dashboard = await _dashboardService.GetAsync(admin, cancellationToken);

            if (admin && dashboard.PendingPayments.Any(p => p.Hn is not null || p.PatientName is not null))
            {
                _logger.LogInformation("AUDIT: Admin {User} viewed patient identity (HN/name) on finance dashboard from {Ip}, {Count} patients",
                    User.Identity?.Name, HttpContext.Connection.RemoteIpAddress, dashboard.PendingPayments.Count);
            }

            return dashboard;
        }
    }
}
