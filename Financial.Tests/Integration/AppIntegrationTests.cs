using Financial.DTOs.Account;
using Financial.Tests.TestSupport;
using Financial.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.RegularExpressions;

namespace Financial.Tests.Integration
{
    /// <summary>
    /// ทดสอบทั้ง pipeline (middleware, authorization, antiforgery, static assets) โดยไม่ต้องต่อ Auth API/ฐานข้อมูลจริง
    /// </summary>
    public partial class AppIntegrationTests : IClassFixture<AppIntegrationTests.AppFactory>
    {
        public sealed class AppFactory : WebApplicationFactory<Program>
        {
            public Mock<IAuthService> AuthService { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseTestDefaults();
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(AuthService.Object);
                });
            }
        }

        private readonly AppFactory _factory;

        public AppIntegrationTests(AppFactory factory)
        {
            _factory = factory;
            _factory.AuthService
                .Setup(s => s.AuthenticateAsync("it099", "correct", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthResponse { IsSuccess = true, Data = new AuthData { LoginName = "it099", FullName = "ทดสอบ ระบบ", JobPosition = "IT" } });
            _factory.AuthService
                .Setup(s => s.AuthenticateAsync("it099", "wrong", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthResponse { IsSuccess = false });
        }

        private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        [Theory]
        [InlineData("/")]
        [InlineData("/Discharges/IPD")]
        [InlineData("/InterfaceSystem")]
        [InlineData("/Home/SiteMap")]
        public async Task ProtectedPages_Anonymous_RedirectToLogin(string url)
        {
            var response = await Client().GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/Account/Login").And.Contain("ReturnUrl=");
        }

        [Fact]
        public async Task Ajax_Anonymous_Gets401InsteadOfLoginHtml()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/Home/DashboardData");
            request.Headers.Add("X-Requested-With", "fetch");

            var response = await Client().SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task IdentityUiPages_AreNotExposed()
        {
            var response = await Client().GetAsync("/Identity/Account/Register");
            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task LoginPage_WorksOffline_NoExternalAssetsAndNoTestCredentials()
        {
            var html = await Client().GetStringAsync("/Account/Login");

            // ทุก stylesheet/script/font ต้องมาจาก server ตัวเอง (ใช้บน Intranet ที่ไม่มี Internet ได้)
            ExternalAssetRegex().Matches(html).Should().BeEmpty();
            html.Should().NotContain("fonts.googleapis").And.NotContain("cdn");
            html.Should().NotContain("abc12345", "ห้ามแสดงรหัสผ่านทดสอบบนหน้า Login");
            html.Should().Contain("__RequestVerificationToken");
        }

        [Fact]
        public async Task StaticAssets_CssAndKanitFontsAreServedLocally()
        {
            var client = Client();

            var css = await client.GetStringAsync("/css/site.css");
            css.Should().Contain("Kanit").And.Contain("kanit-thai-400-normal.woff2");

            foreach (var font in new[] { "kanit-thai-400-normal.woff2", "kanit-latin-600-normal.woff2" })
            {
                (await client.GetAsync($"/fonts/kanit/{font}")).StatusCode.Should().Be(HttpStatusCode.OK, font);
            }

            (await client.GetAsync("/lib/three/three.module.min.js")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/lib/three/addons/OrbitControls.js")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/lib/chartjs/chart.umd.min.js")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Icons_AreServedBeforeLogin_AndReferencedByLoginPage()
        {
            var client = Client();

            var html = await client.GetStringAsync("/Account/Login");
            html.Should().Contain("favicon.svg").And.Contain("favicon.ico").And.Contain("apple-touch-icon.png").And.Contain("site.webmanifest");

            foreach (var (path, contentType) in new[]
            {
                ("/favicon.ico", "image/x-icon"),
                ("/favicon.svg", "image/svg+xml"),
                ("/apple-touch-icon.png", "image/png"),
                ("/site.webmanifest", "application/manifest+json"),
            })
            {
                var response = await client.GetAsync(path);
                response.StatusCode.Should().Be(HttpStatusCode.OK, path);
                response.Content.Headers.ContentType!.MediaType.Should().Be(contentType, path);
            }

            // ICO ต้องมีภาพ 3 ขนาด (16/32/48)
            var ico = await client.GetByteArrayAsync("/favicon.ico");
            BitConverter.ToUInt16(ico, 4).Should().Be(3);
        }

        [Fact]
        public async Task LoginPost_WithoutAntiforgeryToken_IsRejected()
        {
            var response = await Client().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = "it099",
                ["Password"] = "correct",
            }));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task LoginPost_WrongPassword_ShowsErrorAndNoCookie()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

            var response = await PostLogin(client, "it099", "wrong");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            (cookies ?? []).Should().NotContain(c => c.StartsWith("Financial-Auth"));
        }

        [Fact]
        public async Task FullFlow_Login_ThenDashboard_ThenLogout()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

            // 1) Login → redirect ไปหน้าที่ขอไว้ พร้อม cookie ที่เป็น HttpOnly + SameSite
            var login = await PostLogin(client, "it099", "correct", returnUrl: "/Home/SiteMap");
            login.StatusCode.Should().Be(HttpStatusCode.Redirect);
            login.Headers.Location!.ToString().Should().Be("/Home/SiteMap");
            var authCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("Financial-Auth"));
            authCookie.Should().Contain("httponly").And.Contain("samesite=lax");

            // 2) Dashboard แสดงผลได้ (ข้อมูลตัวอย่าง + 3D + กราฟ จากไฟล์ในเครื่อง)
            var dashboard = await client.GetAsync("/");
            dashboard.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await dashboard.Content.ReadAsStringAsync();
            html.Should().Contain("ทดสอบ ระบบ").And.Contain("twinCanvas").And.Contain("ข้อมูลตัวอย่าง");
            ExternalAssetRegex().Matches(html).Should().BeEmpty();
            // 3D ต้องมี import map ครบ — tag helper ของ .NET เคยลบ type="importmap" ทิ้ง ทำให้ 3D ไม่แสดง
            html.Should().MatchRegex("""<script type="importmap">\s*\{ "imports": \{ "three": "/lib/three/three\.module\.min\.js""");
            // รายการผู้ป่วยรอชำระเงินแยก OPD / IPD อยู่ใต้ 3D
            html.IndexOf("pendingBodyO", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("twinCanvas", StringComparison.Ordinal));
            html.Should().Contain("pendingBodyI").And.Contain("twinZoomIn");

            var data = await client.GetAsync("/Home/DashboardData");
            data.StatusCode.Should().Be(HttpStatusCode.OK);
            (await data.Content.ReadAsStringAsync()).Should().Contain("\"PendingPayments\"");

            // 3) Logout (ต้องมี antiforgery token) → กลับหน้า Login และเข้า Dashboard ไม่ได้อีก
            var token = ExtractToken(html);
            var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            logout.StatusCode.Should().Be(HttpStatusCode.Redirect);
            logout.Headers.Location!.ToString().Should().Contain("/Account/Login");

            (await client.GetAsync("/")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        private static async Task<HttpResponseMessage> PostLogin(HttpClient client, string username, string password, string? returnUrl = null)
        {
            var page = await client.GetStringAsync("/Account/Login");
            return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ExtractToken(page),
                ["Username"] = username,
                ["Password"] = password,
                ["ReturnUrl"] = returnUrl ?? "",
            }));
        }

        private static string ExtractToken(string html)
        {
            var match = TokenRegex().Match(html);
            match.Success.Should().BeTrue("หน้าเว็บต้องมี antiforgery token");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        [GeneratedRegex("""name="__RequestVerificationToken" type="hidden" value="([^"]+)" """)]
        private static partial Regex TokenRegex();

        // <link href="http..."> หรือ <script src="http..."> = โหลด asset จากภายนอก
        [GeneratedRegex("""<(?:link|script|img)[^>]+(?:href|src)="(?:https?:)?//""", RegexOptions.IgnoreCase)]
        private static partial Regex ExternalAssetRegex();
    }
}
