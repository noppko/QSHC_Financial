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
    /// สิทธิ์ตาม role ทั้ง pipeline: Admin = ทุกหน้า + ตั้งค่าระบบ, Cashier* = เมนูการเงิน, ทั่วไป = Dashboard เท่านั้น
    /// </summary>
    public partial class RoleAccessTests : IClassFixture<RoleAccessTests.Factory>
    {
        public sealed class Factory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseTestDefaults();
                builder.ConfigureServices(services =>
                {
                    var auth = new Mock<IAuthService>();
                    foreach (var (user, roles) in new[] { ("admin1", new[] { "Admin" }), ("cash1", new[] { "CashierOPD" }), ("user1", Array.Empty<string>()) })
                    {
                        auth.Setup(s => s.AuthenticateAsync(user, "pw", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new AuthResponse
                            {
                                IsSuccess = true,
                                Data = new AuthData { LoginName = user, FullName = $"ชื่อ {user}", JobPosition = "ตำแหน่ง", Division = "หน่วยงาน", Gender = "หญิง", Email = $"{user}@qshc.local", Roles = [.. roles] },
                            });
                    }
                    services.AddSingleton(auth.Object);
                });
            }
        }

        private readonly Factory _factory;

        public RoleAccessTests(Factory factory) => _factory = factory;

        private async Task<HttpClient> LoginAs(string username)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var page = await client.GetStringAsync("/Account/Login");
            var token = WebUtility.HtmlDecode(TokenRegex().Match(page).Groups[1].Value);
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Username"] = username,
                ["Password"] = "pw",
            }));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"{username} ต้อง Login ได้");
            return client;
        }

        public static TheoryData<string, string, bool> AccessMatrix => new()
        {
            // user,    url,                         allowed
            { "user1", "/",                          true },
            { "user1", "/Home/DashboardData",        true },
            { "user1", "/Discharges/IPD",            false },
            { "user1", "/Discharges/OPD",            false },
            { "user1", "/InterfaceSystem",           false },
            { "user1", "/InterfaceSystem/EDCIndex",  false },
            { "user1", "/Admin",                     false },
            { "cash1", "/",                          true },
            { "cash1", "/Discharges/IPD",            true },
            { "cash1", "/InterfaceSystem",           true },
            { "cash1", "/Admin",                     false },
            { "cash1", "/Reports/NhsoFiscal",        true },
            { "user1", "/Reports/NhsoFiscal",        false },
            { "admin1", "/Reports/NhsoFiscal?side=O&from=2566&to=2568", true },
            { "admin1", "/",                         true },
            { "admin1", "/Discharges/OPD",           true },
            { "admin1", "/InterfaceSystem",          true },
            { "admin1", "/Admin",                    true },
        };

        [Theory]
        [MemberData(nameof(AccessMatrix))]
        public async Task Pages_RespectRoles(string user, string url, bool allowed)
        {
            var client = await LoginAs(user);

            var response = await client.GetAsync(url);

            if (allowed)
            {
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            else
            {
                response.StatusCode.Should().Be(HttpStatusCode.Redirect);
                response.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");
            }
        }

        [Fact]
        public async Task GeneralUser_AjaxToFinanceEndpoint_Gets403()
        {
            var client = await LoginAs("user1");
            var request = new HttpRequestMessage(HttpMethod.Post, "/InterfaceSystem/TestConnection");
            request.Headers.Add("X-Requested-With", "fetch");

            var response = await client.SendAsync(request);

            // ถูกปฏิเสธก่อนถึง antiforgery/controller
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GeneralUser_SeesOnlyDashboardMenu_AndProfileWithoutRoles()
        {
            var html = await (await LoginAs("user1")).GetStringAsync("/");

            html.Should().Contain("Dashboard การเงิน");
            html.Should().NotContain("Discharge IPD").And.NotContain("นำเข้าไฟล์ EDC").And.NotContain("ตั้งค่าระบบ");
            html.Should().Contain("ผู้ใช้งานทั่วไป").And.Contain("ไม่มี role");
        }

        [Fact]
        public async Task Cashier_SeesFinanceMenu_AndRoleInDropdown()
        {
            var html = await (await LoginAs("cash1")).GetStringAsync("/");

            html.Should().Contain("Discharge IPD").And.Contain("นำเข้าไฟล์ EDC");
            html.Should().NotContain(">ตั้งค่าระบบ<");
            html.Should().Contain("เจ้าหน้าที่การเงิน");
            html.Should().MatchRegex("data-role[^>]*>CashierOPD<");
            // claim จาก API แสดงใน dropdown
            html.Should().Contain("cash1@qshc.local").And.Contain("หน่วยงาน").And.Contain("หญิง");
        }

        [Fact]
        public async Task Admin_SeesSettingsPage_WithoutSecrets()
        {
            var client = await LoginAs("admin1");

            var home = await client.GetStringAsync("/");
            home.Should().Contain("ตั้งค่าระบบ").And.Contain("ผู้ดูแลระบบ");

            var admin = await client.GetStringAsync("/Admin");
            admin.Should().Contain("สิทธิ์การใช้งานตาม Role").And.Contain("HealthObject");
            admin.Should().NotContain("Password=", "ห้ามแสดง connection string เต็ม");
        }

        [Fact]
        public async Task PatientIdentity_AdminSeesHnAndName_OthersNeverReceiveIt()
        {
            var admin = await LoginAs("admin1");
            var cashier = await LoginAs("cash1");
            var general = await LoginAs("user1");

            // JSON ใส่อักษรไทยเป็น \uXXXX จึงแปลงก่อนตรวจ
            static List<System.Text.Json.JsonElement> Pending(string json) =>
                System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("PendingPayments").EnumerateArray().ToList();

            var adminPending = Pending(await admin.GetStringAsync("/Home/DashboardData"));
            adminPending.Should().NotBeEmpty();
            adminPending.Should().OnlyContain(p => p.GetProperty("Hn").GetString()!.StartsWith("69")
                && p.GetProperty("PatientName").GetString()!.StartsWith("(ตัวอย่าง)"));
            (await admin.GetStringAsync("/")).Should().Contain("data-identity=\"true\"").And.Contain("<th>HN</th>");

            foreach (var other in new[] { cashier, general })
            {
                var json = await other.GetStringAsync("/Home/DashboardData");
                json.Should().NotContain("VisitUid");
                var pending = Pending(json);
                pending.Should().NotBeEmpty();
                pending.Should().OnlyContain(p => p.GetProperty("Hn").ValueKind == System.Text.Json.JsonValueKind.Null
                    && p.GetProperty("PatientName").ValueKind == System.Text.Json.JsonValueKind.Null);
                var html = await other.GetStringAsync("/");
                html.Should().Contain("data-identity=\"false\"").And.NotContain("<th>HN</th>").And.NotContain("(ตัวอย่าง)");
            }
        }

        [Fact]
        public async Task SiteMap_ListsOnlyPermittedPages()
        {
            var html = await (await LoginAs("user1")).GetStringAsync("/Home/SiteMap");

            html.Should().Contain("/Home/Index");
            html.Should().NotContain("/Discharges/IPD").And.NotContain("/Admin/Index");
        }

        [GeneratedRegex("""name="__RequestVerificationToken" type="hidden" value="([^"]+)" """)]
        private static partial Regex TokenRegex();
    }
}
