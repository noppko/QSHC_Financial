using Financial.Services;
using Financial.Data;
using Financial.Navigation;
using Financial.Services.Authorization;
using Financial.Services.Notifications;
using Financial.Services.Receivables;
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
        /// <summary>ดึงข้อมูลจาก HIS ได้ไม่เกินครั้งละกี่ปี (1 ปี ≈ หลายนาที)</summary>
        public const int MAX_REFRESH_YEARS = 10;

        private readonly FiscalRefreshQueue _refreshQueue;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AdminController> _logger;
        private readonly ITelegramNotifier? _telegram;
        private readonly IOptions<TelegramOptions>? _telegramOptions;
        private readonly ReceivableSyncQueue? _receivableQueue;
        private readonly ReceivableOptions _receivableOptions;

        public AdminController(
            IConfiguration configuration,
            IOptions<AuthSettings> authSettings,
            IOptions<RoleOptions> roleOptions,
            IOptions<FileUploadOptions> uploadOptions,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IAppSettingsStore settingsStore,
            ApplicationDbContext db,
            FiscalRefreshQueue refreshQueue,
            TimeProvider timeProvider,
            ILogger<AdminController> logger,
            ITelegramNotifier? telegram = null,
            IOptions<TelegramOptions>? telegramOptions = null,
            ReceivableSyncQueue? receivableQueue = null,
            IOptions<ReceivableOptions>? receivableOptions = null)
        {
            _receivableQueue = receivableQueue;
            _receivableOptions = receivableOptions?.Value ?? new ReceivableOptions();
            _telegram = telegram;
            _telegramOptions = telegramOptions;
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
                AppMenu.Groups.Select(g => new MenuGroupSetting(g.Key, g.Title, g.Items.SelectMany(i => i.Pages()).Select(p => p.FullName).ToList(), g.CanHide, !AppMenuFilter.IsHidden(g, setting))).ToList(),
                setting,
                summaries,
                summaryError,
                _refreshQueue.Statuses,
                Financial.Services.Dashboard.ThaiClock.Now(_timeProvider));
            // สถานะ Telegram: แสดงเฉพาะว่าตั้งค่าแล้วหรือยัง + chat id แบบปิดบัง (ไม่แสดง token)
            var telegram = _telegramOptions?.Value ?? new TelegramOptions();
            ViewData["TelegramConfigured"] = telegram.IsConfigured;
            ViewData["TelegramEnabled"] = telegram.Enabled;
            ViewData["TelegramChat"] = telegram.IsConfigured ? telegram.MaskedChatId : null;
            ViewData["TelegramProxy"] = !string.IsNullOrWhiteSpace(telegram.Proxy);
            ViewData["TelegramOnSuccess"] = telegram.NotifyRefreshSuccess;
            ViewData["ReceivableStartDate"] = _receivableOptions.EffectiveStartDate(model.Now);
            ViewData["ReceivableSyncStatus"] = _receivableQueue?.Status;
            return View(model);
        }

        /// <summary>
        /// ดึงลูกหนี้สิทธิจาก HIS ตามช่วงวันออกบิล (ทำงานเบื้องหลัง) — ไม่ก่อนวันเริ่มตั้งหนี้ (Receivables:StartDate), ไม่เกินวันนี้
        /// ใช้ดึงใหม่เมื่อบิลถูกแก้หลังจำหน่าย (เช่น เภสัชคีย์ยาเพิ่ม) ที่เก่ากว่ารอบกลางคืน
        /// </summary>
        [HttpPost]
        public IActionResult RefreshReceivables(DateTime? from, DateTime? to)
        {
            if (_receivableQueue is null) return BadRequest(new { message = "ไม่ได้เปิดใช้งานลูกหนี้สิทธิ" });
            var today = Financial.Services.Dashboard.ThaiClock.Now(_timeProvider).Date;
            var start = _receivableOptions.EffectiveStartDate(today);
            var f = from?.Date ?? start;
            var t = to?.Date ?? today;
            if (f > t) (f, t) = (t, f);
            if (f < start) f = start;
            if (t > today) t = today;
            if (t < f) return BadRequest(new { message = $"ช่วงวันที่ต้องอยู่ระหว่าง {start:dd/MM/yyyy} ถึง {today:dd/MM/yyyy}" });

            var queued = _receivableQueue.Enqueue(f, t);
            _logger.LogInformation("Receivable sync {From:yyyy-MM-dd}–{To:yyyy-MM-dd} requested by {User}: {Queued}", f, t, User.Identity?.Name, queued);
            return Json(new { queued, message = queued ? $"เริ่มดึงลูกหนี้ {f:dd/MM/yyyy}–{t:dd/MM/yyyy}" : "มีงานดึงลูกหนี้กำลังทำอยู่", status = _receivableQueue.Status });
        }

        [HttpGet]
        public IActionResult ReceivableSyncStatus() => Json(_receivableQueue?.Status);

        /// <summary>ส่งข้อความทดสอบ Telegram (Admin เท่านั้น) — ข้อความไม่มีข้อมูลผู้ป่วย</summary>
        [HttpPost]
        public async Task<IActionResult> TestTelegram(CancellationToken cancellationToken)
        {
            var result = _telegram is null
                ? new TelegramSendResult(false, "ยังไม่ได้ตั้งค่า Telegram")
                : await _telegram.SendAsync($"🔔 ทดสอบการแจ้งเตือน (ส่งโดย {User.Identity?.Name})", cancellationToken: cancellationToken);
            _logger.LogInformation("Telegram test by {User}: {Sent}", User.Identity?.Name, result.Sent);
            if (result.Sent) TempData["Message"] = "ส่งข้อความทดสอบ Telegram แล้ว — ตรวจในแชทที่ตั้งไว้";
            else TempData["Error"] = "ส่ง Telegram ไม่สำเร็จ: " + result.Reason;
            return Redirect(Url.Action(nameof(Index)) + "#telegram");
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

        /// <summary>ดึงข้อมูลรายงานสิทธิ์จาก HIS หลายปีงบ (ทำงานเบื้องหลังทีละปี — ทุกสิทธิ์ในครั้งเดียว) · Admin เท่านั้น</summary>
        [HttpPost]
        public IActionResult RefreshFiscal(int from, int to)
        {
            var current = FiscalYear.Of(Financial.Services.Dashboard.ThaiClock.Now(_timeProvider)).BuddhistYear;
            if (from > to) (from, to) = (to, from);
            if (from < 2550 || to > current || to - from + 1 > MAX_REFRESH_YEARS)
            {
                return BadRequest(new { message = $"เลือกปีงบ 2550–{current} ได้ไม่เกิน {MAX_REFRESH_YEARS} ปีต่อครั้ง" });
            }

            var queued = new List<int>();
            for (var y = from; y <= to; y++)
            {
                if (_refreshQueue.Enqueue(y)) queued.Add(y);
            }
            _logger.LogInformation("Fiscal summary FY {From}-{To} requested by {User} (queued {Queued})", from, to, User.Identity?.Name, string.Join(",", queued));
            return Json(new { message = queued.Count == 0 ? "ปีที่เลือกอยู่ระหว่างประมวลผลแล้ว" : $"เริ่มดึงข้อมูล {queued.Count} ปีงบ", status = Status(from, to) });
        }

        /// <summary>สถานะการดึงข้อมูลของปีงบที่เลือก (หน้าเว็บเรียกซ้ำทุก 2 วินาทีเพื่อแสดง progress bar)</summary>
        [HttpGet]
        public IActionResult RefreshStatus(int from, int to)
        {
            if (from > to) (from, to) = (to, from);
            if (to - from + 1 > MAX_REFRESH_YEARS) from = to - MAX_REFRESH_YEARS + 1;
            return Json(Status(from, to));
        }

        private object Status(int from, int to)
        {
            var now = DateTime.Now;
            var years = Enumerable.Range(from, to - from + 1).Select(y => _refreshQueue.Get(y)).ToList();
            var tracked = years.Where(s => s is not null).Select(s => s!).ToList();
            return new
            {
                total = tracked.Count,
                done = tracked.Count(s => s.State == Financial.Services.Reports.RefreshStatus.DONE),
                failed = tracked.Count(s => s.State == Financial.Services.Reports.RefreshStatus.FAILED),
                active = tracked.Any(s => s.IsActive),
                years = tracked.OrderBy(s => s.FiscalYear).Select(s => new
                {
                    year = s.FiscalYear,
                    state = s.State,
                    message = s.Message,
                    rows = s.RowsRead,
                    seconds = s.StartedAt is DateTime started ? (int)((s.IsActive ? now : s.UpdatedAt) - started).TotalSeconds : 0,
                }),
            };
        }

        private async Task<(IReadOnlyList<SummaryYearStatus> Rows, string? Error)> LoadSummaryStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _db.FiscalSchemeTotals.AsNoTracking()
                    .GroupBy(s => s.FiscalYear)
                    .Select(g => new SummaryYearStatus(g.Key, g.Count(), g.Sum(x => x.NetAmount), g.Max(x => x.RefreshedAt)))
                    .ToListAsync(cancellationToken);
                return (rows.OrderByDescending(r => r.FiscalYear).ToList(), null);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cannot read FiscalSchemeTotals");
                return ([], "อ่านตาราง FiscalSchemeTotals ไม่ได้ — ตรวจการเชื่อมต่อฐาน QSHC_Financial");
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
