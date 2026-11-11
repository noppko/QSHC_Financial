using Financial.Services;
using Financial.Data;
using Financial.Navigation;
using Financial.Services.Authorization;
using Financial.Services.Reports;
using Financial.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Financial.Controllers
{
    /// <summary>
    /// ตั้งค่าระบบ (Admin เท่านั้น): ดูการตั้งค่า, ทดสอบการเชื่อมต่อ, ล้าง cache
    /// ไม่แสดงรหัสผ่าน/connection string เต็ม
    /// </summary>
    [Authorize(Policy = AppPolicies.ADMIN)]
    public class AdminController : Controller
    {
        public const string HEALTH_CHECK_CLIENT = "health-check";

        /// <summary>ฐานข้อมูลที่อนุญาตให้ทดสอบ (ชื่อ connection string)</summary>
        public static readonly string[] Databases = ["HealthObject", "InterfaceSystem", "DefaultConnection"];

        private readonly IConfiguration _configuration;
        private readonly IOptions<AuthSettings> _authSettings;
        private readonly IOptions<RoleOptions> _roleOptions;
        private readonly IOptions<FileUploadOptions> _uploadOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IAppSettingsStore _settingsStore;
        private readonly ApplicationDbContext _db;
        private readonly NhsoFiscalRefreshQueue _refreshQueue;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IConfiguration configuration,
            IOptions<AuthSettings> authSettings,
            IOptions<RoleOptions> roleOptions,
            IOptions<FileUploadOptions> uploadOptions,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IAppSettingsStore settingsStore,
            ApplicationDbContext db,
            NhsoFiscalRefreshQueue refreshQueue,
            TimeProvider timeProvider,
            ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _authSettings = authSettings;
            _roleOptions = roleOptions;
            _uploadOptions = uploadOptions;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _settingsStore = settingsStore;
            _db = db;
            _refreshQueue = refreshQueue;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var setting = await _settingsStore.GetAsync(cancellationToken);
            var (summaries, summaryError) = await LoadSummaryStatusAsync(cancellationToken);
            var model = new AdminSettingsViewModel(
                _authSettings.Value,
                _roleOptions.Value,
                _uploadOptions.Value,
                _configuration["Dashboard:Source"] ?? "His",
                Databases.Select(name => DescribeConnection(name, _configuration.GetConnectionString(name))).ToList(),
                AppMenu.Groups.Select(g => new MenuGroupSetting(g.Key, g.Title, g.Items.Select(i => i.Name).ToList(), g.CanHide, !AppMenuFilter.IsHidden(g, setting))).ToList(),
                setting,
                summaries,
                summaryError,
                _refreshQueue.Statuses,
                Financial.Services.Dashboard.ThaiClock.Now(_timeProvider));
            return View(model);
        }

        /// <summary>
        /// ทดสอบว่าเข้าถึง Auth API ได้ — ส่ง OPTIONS โดยไม่มี username/password (API ที่ทำงานปกติจะตอบ 405/200)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TestAuthApi(CancellationToken cancellationToken)
        {
            var endpoint = _authSettings.Value.AuthEndpoint;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return Json(new { success = false, message = "AuthSettings:AuthEndpoint ไม่ใช่ URL ที่ถูกต้อง" });
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient(HEALTH_CHECK_CLIENT);
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, uri), cancellationToken);
                var allow = string.Join(", ", response.Content.Headers.Allow);
                return Json(new
                {
                    success = (int)response.StatusCode < 500,
                    message = $"ตอบกลับ HTTP {(int)response.StatusCode} ใน {stopwatch.ElapsedMilliseconds} ms" + (allow.Length > 0 ? $" (Allow: {allow})" : ""),
                });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Auth API health check failed");
                return Json(new { success = false, message = $"เชื่อมต่อไม่ได้ ({stopwatch.ElapsedMilliseconds} ms): {ex.GetType().Name}" });
            }
        }

        /// <summary>
        /// ทดสอบการเชื่อมต่อฐานข้อมูล — เปิด connection อย่างเดียว ไม่รันคำสั่ง SQL
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TestDatabase(string name, CancellationToken cancellationToken)
        {
            if (!Databases.Contains(name))
            {
                return Json(new { success = false, message = "ไม่รู้จักฐานข้อมูลนี้" });
            }

            var connectionString = _configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Json(new { success = false, message = "ยังไม่ได้ตั้งค่า connection string" });
            }

            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync(cancellationToken);
                return Json(new { success = true, message = $"เชื่อมต่อสำเร็จใน {stopwatch.ElapsedMilliseconds} ms (SQL Server {conn.ServerVersion})" });
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Database health check failed for {Name}", name);
                return Json(new { success = false, message = $"เชื่อมต่อไม่ได้ ({stopwatch.ElapsedMilliseconds} ms): error {ex.Number}" });
            }
        }

        /// <summary>
        /// บันทึกการเปิด/ปิดกลุ่มเมนู (มีผลกับทุกผู้ใช้ทันที) — visibleGroups = key ของกลุ่มที่เลือกให้แสดง
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveMenuVisibility(string[]? visibleGroups, CancellationToken cancellationToken)
        {
            var visible = new HashSet<string>(visibleGroups ?? [], StringComparer.OrdinalIgnoreCase);
            var hidden = AppMenu.Groups.Where(g => g.CanHide && !visible.Contains(g.Key)).Select(g => g.Key).ToList();

            try
            {
                var current = await _settingsStore.GetAsync(cancellationToken);
                await _settingsStore.SaveAsync(current with { HiddenMenuGroups = hidden }, cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or InvalidOperationException)
            {
                _logger.LogError(ex, "Cannot save menu visibility");
                TempData["Error"] = "บันทึกไม่สำเร็จ — ตรวจการเชื่อมต่อฐาน QSHC_Financial (DefaultConnection)";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Menu visibility changed by {User}. Hidden groups: {Hidden}", User.Identity?.Name, hidden.Count == 0 ? "(none)" : string.Join(",", hidden));
            TempData["Message"] = hidden.Count == 0 ? "บันทึกแล้ว: แสดงเมนูทุกกลุ่ม" : $"บันทึกแล้ว: ซ่อน {hidden.Count} กลุ่มเมนู";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// สั่งประมวลผลรายงาน สปสช. ของปีงบ (ทำงานเบื้องหลัง: ดึงจาก HIS → บันทึกลง QSHC_Financial)
        /// </summary>
        [HttpPost]
        public IActionResult RefreshNhsoSummary(int fiscalYear)
        {
            var current = FiscalYear.Of(Financial.Services.Dashboard.ThaiClock.Now(_timeProvider)).BuddhistYear;
            if (fiscalYear < 2550 || fiscalYear > current)
            {
                TempData["Error"] = "ปีงบประมาณไม่ถูกต้อง";
            }
            else if (_refreshQueue.Enqueue(fiscalYear))
            {
                _logger.LogInformation("NHSO summary FY {Year} queued by {User}", fiscalYear, User.Identity?.Name);
                TempData["Message"] = $"เริ่มประมวลผลปีงบ {fiscalYear} แล้ว (ทำงานเบื้องหลัง อาจใช้เวลาหลายนาที) — รีเฟรชหน้านี้เพื่อดูสถานะ";
            }
            else
            {
                TempData["Message"] = $"ปีงบ {fiscalYear} อยู่ระหว่างประมวลผล";
            }

            return Redirect(Url.Action(nameof(Index)) + "#nhso-summary");
        }

        private async Task<(IReadOnlyList<SummaryYearStatus> Rows, string? Error)> LoadSummaryStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _db.NhsoFiscalSummaries.AsNoTracking()
                    .GroupBy(s => s.FiscalYear)
                    .Select(g => new SummaryYearStatus(g.Key, g.Count(), g.Sum(x => x.NetAmount), g.Max(x => x.RefreshedAt)))
                    .ToListAsync(cancellationToken);
                return (rows.OrderByDescending(r => r.FiscalYear).ToList(), null);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot read NhsoFiscalSummaries");
                return ([], "อ่านตาราง NhsoFiscalSummaries ไม่ได้ — ตรวจการเชื่อมต่อฐาน QSHC_Financial");
            }
        }

        /// <summary>
        /// ล้าง cache ของ Dashboard/Discharge เพื่อให้ดึงข้อมูลใหม่ทันที
        /// </summary>
        [HttpPost]
        public IActionResult ClearCache()
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }

            _logger.LogInformation("Cache cleared by {User}", User.Identity?.Name);
            TempData["Message"] = "ล้าง cache เรียบร้อย ข้อมูลจะถูกดึงใหม่ในการเปิดหน้าครั้งถัดไป";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>แสดงเฉพาะ server/database/user — ไม่แสดงรหัสผ่าน</summary>
        internal static DatabaseInfo DescribeConnection(string name, string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return new DatabaseInfo(name, "-", "-", "-", false);
            }

            try
            {
                var b = new SqlConnectionStringBuilder(connectionString);
                var user = b.IntegratedSecurity ? "Windows Authentication" : b.UserID;
                return new DatabaseInfo(name, b.DataSource, b.InitialCatalog, user, !b.IntegratedSecurity && string.Equals(b.UserID, "sa", StringComparison.OrdinalIgnoreCase));
            }
            catch (ArgumentException)
            {
                return new DatabaseInfo(name, "(รูปแบบไม่ถูกต้อง)", "-", "-", false);
            }
        }
    }

    public sealed record DatabaseInfo(string Name, string Server, string Database, string User, bool UsesSaAccount);

    public sealed record AdminSettingsViewModel(
        AuthSettings Auth,
        RoleOptions Roles,
        FileUploadOptions Upload,
        string DashboardSource,
        IReadOnlyList<DatabaseInfo> Databases,
        IReadOnlyList<MenuGroupSetting> MenuGroups,
        MenuVisibilitySettings Setting,
        IReadOnlyList<SummaryYearStatus> NhsoSummaries,
        string? NhsoSummaryError,
        IReadOnlyList<RefreshStatus> RefreshQueue,
        DateTime Now);

    public sealed record SummaryYearStatus(int FiscalYear, int Rows, decimal NetAmount, DateTime RefreshedAt);

    public sealed record MenuGroupSetting(string Key, string Title, IReadOnlyList<string> Items, bool CanHide, bool Visible);
}
