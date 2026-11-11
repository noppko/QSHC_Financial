using Financial.DTOs.Account;
using Financial.Models;
using Financial.Services;
using Financial.Services.Authorization;
using FluentAssertions;
using System.Security.Claims;
using System.Text.Json;

namespace Financial.Tests.Services
{
    public class AppPoliciesTests
    {
        private static readonly RoleOptions Options = new() { AdminRoles = ["Admin"], FinanceRolePrefixes = ["Cashier", "Cashire"] };

        private static ClaimsPrincipal UserWithRoles(params string[] roles)
            => UserClaimsFactory.Create(new AuthData { LoginName = "u", Roles = [.. roles] });

        [Theory]
        [InlineData(AccessLevelCodes.ADMIN, "Admin")]
        [InlineData(AccessLevelCodes.ADMIN, "admin")]
        [InlineData(AccessLevelCodes.ADMIN, "Admin", "CashierOPD")]
        [InlineData(AccessLevelCodes.FINANCE, "Cashier")]
        [InlineData(AccessLevelCodes.FINANCE, "CashierOPD")]
        [InlineData(AccessLevelCodes.FINANCE, "cashier_ipd")]
        [InlineData(AccessLevelCodes.FINANCE, "CashireIPD")]
        [InlineData(AccessLevelCodes.FINANCE, "Nurse", "CashierFinance")]
        [InlineData(AccessLevelCodes.GENERAL)]
        [InlineData(AccessLevelCodes.GENERAL, "Nurse")]
        [InlineData(AccessLevelCodes.GENERAL, "HeadCashier")]   // ต้อง "ขึ้นต้น" ด้วย Cashier เท่านั้น
        [InlineData(AccessLevelCodes.GENERAL, "Administrator")] // ต้องตรงกับ Admin ทั้งคำ
        public void GetAccessLevel_FollowsRoleRules(string expected, params string[] roles)
        {
            AppPolicies.GetAccessLevel(UserWithRoles(roles), Options).Should().Be(expected);
        }

        [Fact]
        public void AnonymousUser_HasNoAccess()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")])); // ไม่มี authenticationType = ยังไม่ Login
            AppPolicies.IsAdmin(anonymous, Options).Should().BeFalse();
            AppPolicies.IsFinance(anonymous, Options).Should().BeFalse();
        }

        [Fact]
        public void FinancePrefixes_AreConfigurable()
        {
            var custom = new RoleOptions { AdminRoles = ["SysAdmin"], FinanceRolePrefixes = ["FIN"] };

            AppPolicies.GetAccessLevel(UserWithRoles("FIN-OPD"), custom).Should().Be(AccessLevelCodes.FINANCE);
            AppPolicies.GetAccessLevel(UserWithRoles("Cashier"), custom).Should().Be(AccessLevelCodes.GENERAL);
            AppPolicies.GetAccessLevel(UserWithRoles("SysAdmin"), custom).Should().Be(AccessLevelCodes.ADMIN);
        }

        [Fact]
        public void AuthResponse_DeserializesSpecJson()
        {
            // รูปแบบตาม spec ของ POST /QSHCAuth/api/Account/HOAuthJson
            const string JSON_TEXT = """
            {
              "isSuccess": true,
              "status": "200",
              "message": "Success",
              "data": {
                "loginName": "it099",
                "fullName": "ทดสอบ ระบบ",
                "gender": "ชาย",
                "jobPosition": "นักวิชาการคอมพิวเตอร์",
                "division": "ฝ่ายเทคโนโลยีสารสนเทศ",
                "roles": ["Admin", "CashierOPD"],
                "email": "it099@hospital.go.th"
              }
            }
            """;

            var response = JsonSerializer.Deserialize<AuthResponse>(JSON_TEXT, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var principal = UserClaimsFactory.Create(response.Data!);

            response.IsSuccess.Should().BeTrue();
            principal.FindFirstValue("Gender").Should().Be("ชาย");
            principal.FindFirstValue("JobPosition").Should().Be("นักวิชาการคอมพิวเตอร์");
            principal.FindFirstValue(ClaimTypes.Email).Should().Be("it099@hospital.go.th");
            AppPolicies.Roles(principal).Should().Equal("Admin", "CashierOPD");
            AppPolicies.GetAccessLevel(principal, Options).Should().Be(AccessLevelCodes.ADMIN);
        }

        [Fact]
        public void AuthResponse_NullRoles_TreatedAsGeneralUser()
        {
            const string JSON_TEXT = """{ "isSuccess": true, "data": { "loginName": "u2", "roles": null } }""";

            var response = JsonSerializer.Deserialize<AuthResponse>(JSON_TEXT, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            AppPolicies.GetAccessLevel(UserClaimsFactory.Create(response.Data!), Options).Should().Be(AccessLevelCodes.GENERAL);
        }
    }
}
