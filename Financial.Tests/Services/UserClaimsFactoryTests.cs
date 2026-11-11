using Financial.DTOs.Account;
using Financial.Services;
using FluentAssertions;
using System.Security.Claims;

namespace Financial.Tests.Services
{
    public class UserClaimsFactoryTests
    {
        [Fact]
        public void Create_MapsUserDataToClaims()
        {
            var principal = UserClaimsFactory.Create(new AuthData
            {
                LoginName = " it099 ",
                FullName = "ทดสอบ ระบบ",
                JobPosition = "นักวิชาการคอมพิวเตอร์",
                Division = "ฝ่ายเทคโนโลยีสารสนเทศ",
                Email = "it099@hospital.go.th",
                Roles = ["Admin", "Finance"],
            });

            principal.Identity!.IsAuthenticated.Should().BeTrue();
            principal.Identity.Name.Should().Be("it099");
            principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("it099");
            principal.FindFirstValue("FullName").Should().Be("ทดสอบ ระบบ");
            principal.FindFirstValue("Division").Should().Be("ฝ่ายเทคโนโลยีสารสนเทศ");
            principal.FindFirstValue(ClaimTypes.Email).Should().Be("it099@hospital.go.th");
            principal.IsInRole("Admin").Should().BeTrue();
            principal.IsInRole("Finance").Should().BeTrue();
        }

        [Fact]
        public void Create_SkipsBlankAndDuplicateRoles()
        {
            var principal = UserClaimsFactory.Create(new AuthData
            {
                LoginName = "u1",
                Roles = ["Admin", " ", "", "admin", "Finance"],
            });

            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().BeEquivalentTo(["Admin", "Finance"]);
        }

        [Fact]
        public void Create_MissingOptionalFields_UsesSafeDefaults()
        {
            var principal = UserClaimsFactory.Create(new AuthData { LoginName = "u2", FullName = "", Email = "" });

            principal.FindFirstValue("FullName").Should().Be("u2");
            principal.FindFirstValue("JobPosition").Should().Be("-");
            principal.FindFirst(ClaimTypes.Email).Should().BeNull("ไม่ควรสร้างอีเมลปลอม");
        }

        [Fact]
        public void Create_NullData_Throws()
        {
            var act = () => UserClaimsFactory.Create(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
