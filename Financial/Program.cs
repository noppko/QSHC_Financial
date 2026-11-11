using Financial.Controllers;
using Financial.Data;
using Financial.Database.InterfaceSystem.Contexts;
using Financial.Services;
using Financial.Services.Authorization;
using Financial.Services.Dashboard;
using Financial.Services.Discharges;
using Financial.Services.Reports;
using Financial.Services.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==================== Serilog Configuration ====================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting Financial application");

try
{
    // ==================== Database Contexts ====================
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    var interfaceSystemConnectionString = builder.Configuration.GetConnectionString("InterfaceSystem")
        ?? throw new InvalidOperationException("Connection string 'InterfaceSystem' not found.");

    // QSHC_Financial (10.67.67.162) — ข้อมูลของระบบ Financial เอง (การตั้งค่า, ผลสรุปรายงาน, ReferenceValues) แยกจาก HIS
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    builder.Services.AddDbContext<InterfaceSystemDbContext>(options =>
        options.UseSqlServer(interfaceSystemConnectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // หมายเหตุ: ไม่ใช้ ASP.NET Core Identity UI — ผู้ใช้ยืนยันตัวตนผ่าน External Auth API + Cookie เท่านั้น

    // ==================== Authentication ====================
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "Financial-Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Intranet ใช้ได้ทั้ง HTTP/HTTPS — ถ้าเข้าผ่าน HTTPS cookie จะเป็น Secure อัตโนมัติ
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.LogoutPath = "/Account/Logout";

            // เรียกผ่าน fetch/AJAX → ตอบ 401/403 แทนการ redirect เป็นหน้า HTML
            options.Events.OnRedirectToLogin = context => RespondOrRedirect(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RespondOrRedirect(context, StatusCodes.Status403Forbidden);
        });

    // สิทธิ์ตาม role จาก Auth API: Admin = ทุกหน้า + ตั้งค่าระบบ, Cashier* = เมนูการเงิน, ทั่วไป = Dashboard
    var roleOptions = builder.Configuration.GetSection(RoleOptions.SECTION_NAME).Get<RoleOptions>() ?? new RoleOptions();
    builder.Services.Configure<RoleOptions>(builder.Configuration.GetSection(RoleOptions.SECTION_NAME));
    builder.Services.AddAuthorization(options => AppPolicies.Configure(options, roleOptions));

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "RequestVerificationToken";
        options.Cookie.Name = "Financial-Antiforgery";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // ==================== Rate Limiting (ป้องกันการเดารหัสผ่าน) ====================
    var loginAttemptsPerMinute = builder.Configuration.GetValue("Security:LoginAttemptsPerMinute", 10);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(AccountController.LOGIN_RATE_LIMIT_POLICY, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    });

    // ==================== Infrastructure ====================
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton(TimeProvider.System);
    // ส่งตัวอักษรไทยตรงๆ ใน HTML แทน &#xE0A; (HTML เล็กลงหลายเท่า) — อักขระอันตรายอย่าง < > " & ยังถูก escape เหมือนเดิม
    builder.Services.Configure<WebEncoderOptions>(options =>
        options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
    builder.Services.AddResponseCompression(options =>
    {
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    });

    // ==================== HttpClient for Auth API ====================
    var authSettings = builder.Configuration.GetSection("AuthSettings").Get<AuthSettings>()
        ?? new AuthSettings();

    builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));

    // Typed client: ห้ามลงทะเบียน IAuthService ซ้ำด้วย AddScoped ไม่เช่นนั้น timeout/retry จะหายไป
    builder.Services.AddHttpClient<IAuthService, AuthService>()
        .AddStandardResilienceHandler(options =>
        {
            // Login เป็น POST ที่ผู้ใช้รออยู่ — retry แค่ครั้งเดียวและจำกัดเวลารวม
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(Math.Max(5, authSettings.TimeoutSeconds / 2));
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
            options.CircuitBreaker.SamplingDuration = options.AttemptTimeout.Timeout * 2;
        });

    // ใช้ทดสอบการเชื่อมต่อในหน้าตั้งค่าระบบ (ไม่ retry, timeout สั้น)
    builder.Services.AddHttpClient(AdminController.HEALTH_CHECK_CLIENT, client => client.Timeout = TimeSpan.FromSeconds(5));

    // ==================== Services Registration ====================
    builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection(FileUploadOptions.SECTION_NAME));
    builder.Services.AddSingleton<IAppSettingsStore, DbAppSettingsStore>();
    builder.Services.AddScoped<IReferenceValueService, ReferenceValueService>();
    builder.Services.AddScoped<IDischargeService, DischargeService>();
    builder.Services.Configure<NhsoReportOptions>(builder.Configuration.GetSection(NhsoReportOptions.SECTION_NAME));
    builder.Services.AddScoped<INhsoFiscalReportService, NhsoFiscalReportService>();
    builder.Services.AddScoped<INhsoHisSource, NhsoHisSource>();
    builder.Services.AddScoped<NhsoFiscalSummaryRefresher>();
    builder.Services.AddSingleton<NhsoFiscalRefreshQueue>();
    builder.Services.AddHostedService<NhsoFiscalRefreshWorker>();
    // Dashboard:Source = "His" (HealthObject จริง, ค่าเริ่มต้น) หรือ "Sample" (ข้อมูลตัวอย่างสำหรับทดสอบหน้าจอ)
    if (string.Equals(builder.Configuration["Dashboard:Source"], "Sample", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddScoped<IFinanceDashboardService, SampleFinanceDashboardService>();
    }
    else
    {
        builder.Services.AddScoped<IFinanceDashboardService, HisFinanceDashboardService>();
    }

    // ==================== MVC ====================
    builder.Services.AddControllersWithViews(options =>
        {
            // ตรวจ Antiforgery token ทุก POST (ฟอร์มใช้ tag helper, fetch ส่ง header RequestVerificationToken)
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null; // ใช้ PascalCase
        });

    // ==================== Localization (Thai Culture) ====================
    var thaiCulture = new CultureInfo("th-TH");
    // ใช้ Gregorian Calendar สำหรับ database และ API (ค.ศ.)
    thaiCulture.DateTimeFormat.Calendar = new GregorianCalendar();

    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture(thaiCulture);
        options.SupportedCultures = [thaiCulture];
        options.SupportedUICultures = [thaiCulture];
    });

    var app = builder.Build();

    // ==================== Database Migration (QSHC_Financial) ====================
    // สร้าง/อัปเดตตารางของระบบ Financial อัตโนมัติ (Database:AutoMigrate) — ถ้าเชื่อมต่อฐานไม่ได้ แอปยังเปิดได้และแจ้งใน log
    if (app.Configuration.GetValue("Database:AutoMigrate", true))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            if (db.Database.IsRelational())
            {
                db.Database.Migrate();
                Log.Information("QSHC_Financial database is up to date");
            }
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            Log.Error(ex, "Cannot migrate QSHC_Financial database (DefaultConnection) — settings/report summary will be unavailable");
        }
    }

    // ==================== Middleware Pipeline ====================
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseResponseCompression();

    // Localization Middleware
    app.UseRequestLocalization();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // .NET 10: static assets ถูก fingerprint + บีบอัดตอน build และ cache ที่ browser ได้นาน
    // AllowAnonymous: หน้า Login ต้องโหลด CSS/ฟอนต์ได้ก่อนเข้าสู่ระบบ (fallback policy บังคับ login ทุก endpoint)
    app.MapStaticAssets().AllowAnonymous();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    Log.Information("Financial application started successfully");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}

static Task RespondOrRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    var request = context.Request;
    var isAjax = request.Headers.XRequestedWith == "XMLHttpRequest"
        || request.Headers.XRequestedWith == "fetch"
        || request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    if (isAjax)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program { }
