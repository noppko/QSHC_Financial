using Financial.Data;
using Financial.Database.InterfaceSystem.Contexts;
using Financial.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Globalization;

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

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddDbContext<InterfaceSystemDbContext>(options =>
        options.UseSqlServer(interfaceSystemConnectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // ==================== Identity ====================
    builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

    // ==================== Cookie Policy ====================
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.MinimumSameSitePolicy = SameSiteMode.None;
        options.HttpOnly = HttpOnlyPolicy.Always;
        options.Secure = CookieSecurePolicy.Always;
    });

    // ==================== Authentication ====================
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "Financial-Auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.SlidingExpiration = true;
    });

    // ==================== HttpContextAccessor ====================
    builder.Services.AddHttpContextAccessor();

    // ==================== Memory Cache ====================
    builder.Services.AddMemoryCache();

    // ==================== HttpClient for Auth API ====================
    var authSettings = builder.Configuration.GetSection("AuthSettings").Get<AuthSettings>()
        ?? new AuthSettings();

    builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));

    builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
    {
        client.BaseAddress = new Uri("http://10.67.67.166/");
        client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

    // ==================== Services Registration ====================
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IReferenceValueService, ReferenceValueService>();
    //builder.Services.AddScoped<IDashboardService, DashboardService>();

    // ==================== MVC ====================
    builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null; // ใช้ PascalCase
        });

    builder.Services.AddRazorPages();

    // ==================== Localization (Thai Culture) ====================
    var thaiCulture = new CultureInfo("th-TH");
    // ใช้ Gregorian Calendar สำหรับ database และ API (ค.ศ.)
    thaiCulture.DateTimeFormat.Calendar = new GregorianCalendar();

    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture(thaiCulture);
        options.SupportedCultures = new[] { thaiCulture };
        options.SupportedUICultures = new[] { thaiCulture };
    });

    var app = builder.Build();

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
    app.UseStaticFiles();

    // Localization Middleware
    app.UseRequestLocalization();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    Log.Information("Financial application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}

// ==================== Polly Policies ====================
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
