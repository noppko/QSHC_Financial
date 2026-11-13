using Financial.Database.InterfaceSystem.Contexts;
using Financial.Database.InterfaceSystem.Models;
using Financial.DTOs.Account;
using Financial.Services;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Text.RegularExpressions;

namespace Financial.Tests.Integration
{
    /// <summary>
    /// หน้า EDC › ข้อมูล ผ่าน HTTP จริง (culture th-TH) — ตัวกรอง PostDate และ Code
    /// InterfaceSystem แทนด้วย EF InMemory (ไม่แตะฐานจริง)
    /// </summary>
    public partial class EdcFilterTests : IClassFixture<EdcFilterTests.Factory>
    {
        public sealed class Factory : WebApplicationFactory<Program>
        {
            private readonly string _db = "edc-" + Guid.NewGuid().ToString("N");

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseTestDefaults();
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<InterfaceSystemDbContext>>();
                    services.RemoveAll(typeof(IDbContextOptionsConfiguration<InterfaceSystemDbContext>));
                    services.AddDbContext<InterfaceSystemDbContext>(o => o.UseInMemoryDatabase(_db));

                    var auth = new Mock<IAuthService>();
                    auth.Setup(s => s.AuthenticateAsync("cash1", "pw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new AuthResponse { IsSuccess = true, Data = new AuthData { LoginName = "cash1", FullName = "ชื่อ", Roles = ["CashierOPD"] } });
                    services.AddSingleton(auth.Object);
                });
            }

            public async Task SeedAsync(params PatientEdcregister[] rows)
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<InterfaceSystemDbContext>();
                if (await db.PatientEdcregisters.AnyAsync()) return;
                db.PatientEdcregisters.AddRange(rows);
                await db.SaveChangesAsync();
            }
        }

        private readonly Factory _factory;

        public EdcFilterTests(Factory factory) => _factory = factory;

        [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
        private static partial Regex TokenRegex();

        private static PatientEdcregister Edc(int uid, string code, DateTime post)
            => new() { Uid = uid, Code = code, PostDate = post, TransactionDate = post, PersonalId = "0", AppCode = "A", StatusFlag = "A", Timestamp = [] };

        private async Task<HttpClient> CashierAsync()
        {
            await _factory.SeedAsync(
                Edc(1, "FA0014584", new DateTime(2026, 11, 9, 17, 53, 0)),
                Edc(2, "ZZ0000001", new DateTime(2026, 11, 12, 8, 0, 0)));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var page = await client.GetStringAsync("/Account/Login");
            var token = WebUtility.HtmlDecode(TokenRegex().Match(page).Groups[1].Value);
            var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["Username"] = "cash1", ["Password"] = "pw",
            }));
            login.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return client;
        }

        [Theory]
        // ค่าที่ <input type="date"> ส่งมา = yyyy-MM-dd เสมอ
        [InlineData("postFrom=2026-11-09&postTo=2026-11-09", true, false)]
        [InlineData("postFrom=2026-11-09", true, true)]
        [InlineData("postTo=2026-11-10", true, false)]
        [InlineData("postFrom=2026-11-10&postTo=2026-11-11", false, false)]
        [InlineData("code=FA0014584", true, false)]
        [InlineData("code=FA0014584&postFrom=2026-11-09&postTo=2026-11-09", true, false)]
        [InlineData("code=FA0014584&postFrom=2026-11-12", false, false)]
        [InlineData("code=4584", true, false)]
        [InlineData("code=FA00", true, false)]
        public async Task FiltersByPostDateAndCode(string query, bool expectFa, bool expectZz)
        {
            var html = await (await CashierAsync()).GetStringAsync("/InterfaceSystem/EDCIndex?" + query);

            html.Contains("<td>FA0014584</td>").Should().Be(expectFa, query);
            html.Contains("<td>ZZ0000001</td>").Should().Be(expectZz, query);
        }

        [Fact]
        public async Task NoResult_WithCodeAndDate_TellsWhichDatesTheCodeHas()
        {
            var client = await CashierAsync();

            var wrongDate = await client.GetStringAsync("/InterfaceSystem/EDCIndex?code=FA0014584&postFrom=2026-11-10&postTo=2026-11-11");
            wrongDate.Should().Contain("มี 1 รายการ PostDate ระหว่าง 9/11/2026 – 9/11/2026");

            var unknown = await client.GetStringAsync("/InterfaceSystem/EDCIndex?code=FA0014583&postFrom=2026-11-09&postTo=2026-11-10");
            unknown.Should().Contain("ไม่พบ Code “FA0014583” ในทุกช่วงวันที่");
        }

        [Fact]
        public async Task KeepsFilterValues_InGregorianIsoFormat()
        {
            var html = await (await CashierAsync()).GetStringAsync("/InterfaceSystem/EDCIndex?postFrom=2026-11-09&postTo=2026-11-10&code=FA0014584");

            html.Should().Contain("value=\"2026-11-09\"").And.Contain("value=\"2026-11-10\"").And.Contain("value=\"FA0014584\"");
            html.Should().NotContain("maxlength=\"5\"", "Code จริงยาวกว่า 5 ตัว เช่น FA0014584");
        }
    }
}
