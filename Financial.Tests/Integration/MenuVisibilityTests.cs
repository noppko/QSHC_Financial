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
    /// Admin เปิด/ปิดกลุ่มเมนูจากหน้าตั้งค่าระบบ → มีผลกับผู้ใช้อื่นทันที
    /// </summary>
    public partial class MenuVisibilityTests : IClassFixture<MenuVisibilityTests.Factory>
    {
        public sealed class Factory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseTestDefaults();
                builder.ConfigureServices(services =>
                {
                    var auth = new Mock<IAuthService>();
                    foreach (var (user, role) in new[] { ("admin1", "Admin"), ("cash1", "CashierOPD") })
                    {
                        auth.Setup(s => s.AuthenticateAsync(user, "pw", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new AuthResponse { IsSuccess = true, Data = new AuthData { LoginName = user, Roles = [role] } });
                    }
                    services.AddSingleton(auth.Object);
                });
            }
        }

        private readonly Factory _factory;

        public MenuVisibilityTests(Factory factory) => _factory = factory;

        private async Task<HttpClient> LoginAs(string username)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var page = await client.GetStringAsync("/Account/Login");
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page),
                ["Username"] = username,
                ["Password"] = "pw",
            }));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return client;
        }

        private static string Token(string html) => WebUtility.HtmlDecode(TokenRegex().Match(html).Groups[1].Value);

        private static async Task<HttpResponseMessage> Save(HttpClient client, params string[] visibleGroups)
        {
            var page = await client.GetStringAsync("/Admin");
            var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", Token(page)) };
            form.AddRange(visibleGroups.Select(g => new KeyValuePair<string, string>("visibleGroups", g)));
            return await client.PostAsync("/Admin/SaveMenuVisibility", new FormUrlEncodedContent(form));
        }

        [Fact]
        public async Task AdminHidesGroup_CashierMenuUpdates_ButPageStillAccessible()
        {
            var admin = await LoginAs("admin1");
            var cashier = await LoginAs("cash1");

            (await cashier.GetStringAsync("/")).Should().Contain("Discharge IPD");

            // ซ่อนกลุ่ม "ผู้ป่วยจำหน่าย" (ไม่ส่ง discharge) และพยายามซ่อนกลุ่มผู้ดูแลระบบด้วย (ไม่ส่ง admin)
            var saved = await Save(admin, "overview", "reports", "interface", "receivables", "other");
            saved.StatusCode.Should().Be(HttpStatusCode.Redirect);

            var cashierHome = await cashier.GetStringAsync("/");
            cashierHome.Should().NotContain("Discharge IPD").And.Contain("นำเข้าไฟล์ EDC");
            (await cashier.GetAsync("/Discharges/IPD")).StatusCode.Should().Be(HttpStatusCode.OK, "ซ่อนจากเมนูเท่านั้น สิทธิ์ยังเป็นไปตาม role");

            var adminHome = await admin.GetStringAsync("/");
            adminHome.Should().Contain("ตั้งค่าระบบ", "กลุ่มผู้ดูแลระบบซ่อนไม่ได้");

            var adminPage = await admin.GetStringAsync("/Admin");
            adminPage.Should().Contain("ซ่อน 1 กลุ่มเมนู").And.Contain("admin1");

            // เปิดกลับ
            await Save(admin, "overview", "discharge", "reports", "interface", "receivables", "other");
            (await cashier.GetStringAsync("/")).Should().Contain("Discharge IPD");
        }

        [Fact]
        public async Task NonAdmin_CannotChangeMenuVisibility()
        {
            var cashier = await LoginAs("cash1");
            var page = await cashier.GetStringAsync("/");

            var response = await cashier.PostAsync("/Admin/SaveMenuVisibility", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page),
                ["visibleGroups"] = "overview",
            }));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");
            (await cashier.GetStringAsync("/")).Should().Contain("Discharge IPD");
        }

        [GeneratedRegex("""name="__RequestVerificationToken" type="hidden" value="([^"]+)" """)]
        private static partial Regex TokenRegex();
    }
}
