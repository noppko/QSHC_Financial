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
            { "cash1", "/Reports/CsmbsFiscal",       true },
            { "cash1", "/Reports/SsoFiscal?side=O",  true },
            { "admin1", "/Admin/RefreshStatus?from=2567&to=2569", true },
            { "cash1", "/Admin/RefreshStatus?from=2567&to=2569", false },
            { "user1", "/Admin/RefreshStatus?from=2567&to=2569", false },
            { "user1", "/Reports/CsmbsFiscal",       false },
            { "user1", "/Reports/SsoFiscal",         false },
            { "cash1", "/Reports/LgoFiscal",         true },
            { "user1", "/Reports/LgoFiscal",         false },
            { "admin1", "/Reports/NhsoFiscal?side=O&from=2566&to=2568", true },
            { "admin1", "/",                         true },
            { "admin1", "/Discharges/OPD",           true },
            { "admin1", "/InterfaceSystem",          true },
            { "admin1", "/Admin",                    true },
            { "admin1", "/ReferenceValues",          true },
            { "admin1", "/ReferenceValues/Create?category=PAY_MODE", true },
            { "cash1", "/ReferenceValues",           false },
            { "user1", "/ReferenceValues",           false },
            // ส่วนงานลูกหนี้สิทธิ (Area Receivables)
            { "cash1", "/Receivables",               true },
            { "cash1", "/Receivables/Home/Index?scheme=LGO", true },
            { "cash1", "/Receivables/Receivables?scheme=CSMBS&aging=91-180", true },
            { "admin1", "/Receivables",              true },
            { "admin1", "/Receivables/Receivables",  true },
            { "admin1", "/Receivables/Home/SyncStatus", true },
            { "cash1", "/Receivables/Home/SyncStatus", false },
            { "user1", "/Receivables",               false },
            { "user1", "/Receivables/Receivables",   false },
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
        public async Task Sidebar_ReportsAreTwoLevels_FiscalYearThenScheme()
        {
            var html = await (await LoginAs("cash1")).GetStringAsync("/Reports/CsmbsFiscal");

            // ระดับ 1 "ตามปีงบประมาณ" เป็นหัวข้อที่กางออกได้ ระดับ 2 = ชื่อสิทธิ์ (หน้าปัจจุบันกางอยู่แล้ว)
            var group = Regex.Match(html, "<details[^>]*>(?s:.*?)</details>").Value;
            group.Should().Contain("ตามปีงบประมาณ").And.Contain("open");
            group.Should().Contain("/Reports/NhsoFiscal").And.Contain("/Reports/CsmbsFiscal").And.Contain("/Reports/SsoFiscal").And.Contain("/Reports/LgoFiscal");
            Regex.Matches(group, "aria-current=\"page\"").Count.Should().Be(1, "ไฮไลต์เฉพาะสิทธิ์ที่เปิดอยู่");
            html.Should().NotContain("สปสช. ตามปีงบประมาณ", "ชื่อเดิมแบบแบนถูกแทนที่ด้วยเมนู 2 ระดับ");

            var general = await (await LoginAs("user1")).GetStringAsync("/");
            general.Should().NotContain("ตามปีงบประมาณ", "ผู้ใช้ทั่วไปไม่มีสิทธิ์เข้ารายงาน จึงไม่เห็นหัวข้อ");
        }

        [Fact]
        public async Task FiscalReport_VisitColumn_IsOptional()
        {
            var client = await LoginAs("cash1");

            var off = await client.GetStringAsync("/Reports/NhsoFiscal");
            off.Should().NotContain("จำนวน (visit)</th>").And.Contain("แสดงจำนวน (visit)");

            var on = await client.GetStringAsync("/Reports/NhsoFiscal?showVisits=true");
            Regex.Matches(on, Regex.Escape("จำนวน (visit)</th>")).Count.Should().Be(3, "ในเขต / นอกเขต / รวม");
            on.Should().Contain("colspan=\"4\"");
        }

        [Theory]
        [InlineData("/Reports/CsmbsFiscal")]
        [InlineData("/Reports/SsoFiscal")]
        [InlineData("/Reports/LgoFiscal")]
        public async Task FiscalReport_OtherSchemes_HaveNoZoneColumns(string url)
        {
            var html = await (await LoginAs("cash1")).GetStringAsync(url + "?showVisits=true");

            html.Should().NotContain(">ในเขต</th>").And.NotContain(">นอกเขต</th>").And.NotContain(">รวม</th>", "ไม่มีหัวกลุ่ม");
            Regex.Matches(html, Regex.Escape("จำนวน (visit)</th>")).Count.Should().Be(1, "คอลัมน์เดียว");

            (await (await LoginAs("cash1")).GetStringAsync("/Reports/NhsoFiscal")).Should().Contain(">นอกเขต</th>", "สปสช. ยังแยกในเขต/นอกเขต");
        }

        [Fact]
        public async Task Sidebar_Edc_IsTwoLevels_ImportAndData_AndTemplateDownloads()
        {
            var cash = await LoginAs("cash1");
            var html = await cash.GetStringAsync("/InterfaceSystem");

            var group = Regex.Matches(html, "<details[^>]*>(?s:.*?)</details>").Select(m => m.Value).Single(d => d.Contains(">EDC<"));
            group.Should().Contain("open", "อยู่ในหน้านำเข้าไฟล์ จึงกางเมนู EDC อยู่แล้ว").And.Contain(">นำเข้าไฟล์<").And.Contain(">ข้อมูล<");
            group.Should().Contain("/InterfaceSystem\"").And.Contain("/InterfaceSystem/EDCIndex");
            html.Should().Contain("/InterfaceSystem/DownloadTemplate", "ปุ่มดาวน์โหลด template");

            var file = await cash.GetAsync("/InterfaceSystem/DownloadTemplate");
            file.StatusCode.Should().Be(HttpStatusCode.OK);
            file.Content.Headers.ContentDisposition!.FileName.Should().StartWith("HCG14584");

            (await LoginAs("user1")).GetAsync("/InterfaceSystem/DownloadTemplate").Result.StatusCode.Should().NotBe(HttpStatusCode.OK, "ผู้ใช้ทั่วไปไม่มีสิทธิ์");

            (await cash.GetStringAsync("/Home/SiteMap")).Should().Contain("EDC — นำเข้าไฟล์").And.Contain("EDC — ข้อมูล");
        }

        [Fact]
        public async Task ReceivablesArea_HasOwnSidebar_WithBackLink_AndMainMenuLinksIntoArea()
        {
            var cash = await LoginAs("cash1");

            var home = await cash.GetStringAsync("/");
            home.Should().Contain("href=\"/Receivables\"", "เมนูหลักลิงก์เข้าส่วนงานลูกหนี้").And.NotContain("data-back-to-main");

            var area = await cash.GetStringAsync("/Receivables");
            var nav = Regex.Match(area, "<nav class=\"flex-1(?s:.*?)</nav>").Value;
            nav.Should().Contain("data-back-to-main").And.Contain("กลับระบบหลัก");
            var back = Regex.Match(nav, "<a[^>]*data-back-to-main[^>]*>").Value;
            Regex.Match(back, "href=\"([^\"]*)\"").Groups[1].Value.Should().Be("/", "กลับหน้าแรกของระบบหลัก (ไม่ติด area)");
            nav.Should().Contain("href=\"/Receivables/Receivables\"").And.Contain("ภาพรวมลูกหนี้");
            nav.Should().NotContain("Discharge IPD", "sidebar ใน Area แสดงเฉพาะเมนูลูกหนี้");
            area.Should().Contain("ส่วนงานลูกหนี้สิทธิ");
            area.Should().Contain("action=\"/Account/Logout\"", "ลิงก์ของงานหลักต้องไม่ติด area");
            area.Should().Contain("data-scheme-card=\"NHSO\"").And.Contain("data-scheme-card=\"CSMBS\"").And.Contain("data-scheme-card=\"SSO\"").And.Contain("data-scheme-card=\"LGO\"");
            area.Should().NotContain("id=\"arSync\"", "ปุ่มดึงข้อมูลเฉพาะ Admin");

            var list = await cash.GetStringAsync("/Receivables/Receivables?scheme=SSO");
            list.Should().NotContain("<th>HN</th>").And.Contain("4 ตัวท้าย");
            list.Should().NotContain(">เขต</th>", "เขตเฉพาะ สปสช.");
            (await cash.GetStringAsync("/Receivables/Receivables?scheme=NHSO")).Should().Contain(">เขต</th>");

            (await (await LoginAs("admin1")).GetStringAsync("/Receivables")).Should().Contain("id=\"arSync\"");
            (await (await LoginAs("admin1")).GetStringAsync("/Receivables/Receivables")).Should().Contain("<th>HN</th>");
        }

        [Fact]
        public async Task ReceivablesSync_IsAdminOnly()
        {
            async Task<HttpStatusCode> Post(string user)
            {
                var client = await LoginAs(user);
                var page = await client.GetStringAsync("/");
                var token = Regex.Match(page, "name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
                var request = new HttpRequestMessage(HttpMethod.Post, "/Receivables/Home/Sync") { Content = new FormUrlEncodedContent([new("days", "30")]) };
                request.Headers.Add("RequestVerificationToken", WebUtility.HtmlDecode(token));
                request.Headers.Add("X-Requested-With", "fetch");
                return (await client.SendAsync(request)).StatusCode;
            }

            (await Post("cash1")).Should().Be(HttpStatusCode.Forbidden);
            (await Post("admin1")).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SiteMap_ListsEachSchemeWithParentName()
        {
            var html = await (await LoginAs("cash1")).GetStringAsync("/Home/SiteMap");

            html.Should().Contain("ตามปีงบประมาณ — สปสช.").And.Contain("ตามปีงบประมาณ — กรมบัญชีกลาง").And.Contain("ตามปีงบประมาณ — ประกันสังคม").And.Contain("ตามปีงบประมาณ — อปท.");
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
