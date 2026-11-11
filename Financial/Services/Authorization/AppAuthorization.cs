using Financial.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Financial.Services.Authorization
{
    /// <summary>
    /// การตั้งค่า role จาก appsettings.json (section "Authorization") — role มาจาก External Auth API (data.roles[])
    /// </summary>
    public class RoleOptions
    {
        public const string SECTION_NAME = "Authorization";

        /// <summary>role ผู้ดูแลระบบ: ตั้งค่าระบบได้ และดูได้ทุกหน้า</summary>
        public string[] AdminRoles { get; set; } = ["Admin"];

        /// <summary>role ด้านการเงิน: ชื่อ role ขึ้นต้นด้วยค่าเหล่านี้ (ไม่สนตัวพิมพ์เล็ก/ใหญ่) เข้าเมนูการเงินได้</summary>
        public string[] FinanceRolePrefixes { get; set; } = ["Cashier"];
    }

    public static class AppPolicies
    {
        /// <summary>ผู้ใช้ที่ Login แล้วทุกคน (Dashboard)</summary>
        public const string AUTHENTICATED = "Authenticated";

        /// <summary>เมนูด้านการเงิน: Admin หรือ role ขึ้นต้นด้วย Cashier</summary>
        public const string FINANCE = "Finance";

        /// <summary>ตั้งค่าระบบ: Admin เท่านั้น</summary>
        public const string ADMIN = "Admin";

        public static IEnumerable<string> Roles(ClaimsPrincipal user)
            => user.FindAll(ClaimTypes.Role).Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v));

        public static bool IsAdmin(ClaimsPrincipal user, RoleOptions options)
            => user.Identity?.IsAuthenticated == true
               && Roles(user).Any(r => options.AdminRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase));

        public static bool IsFinance(ClaimsPrincipal user, RoleOptions options)
            => user.Identity?.IsAuthenticated == true
               && Roles(user).Any(r => options.FinanceRolePrefixes.Any(p => !string.IsNullOrWhiteSpace(p) && r.Trim().StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        public static string GetAccessLevel(ClaimsPrincipal user, RoleOptions options)
            => IsAdmin(user, options) ? AccessLevelCodes.ADMIN
             : IsFinance(user, options) ? AccessLevelCodes.FINANCE
             : AccessLevelCodes.GENERAL;

        /// <summary>
        /// ชื่อระดับสิทธิ์สำรอง (ใช้เมื่ออ่าน ReferenceValues category ACCESS_LEVEL ไม่ได้)
        /// </summary>
        public static string Describe(string level) => level switch
        {
            AccessLevelCodes.ADMIN => "ผู้ดูแลระบบ",
            AccessLevelCodes.FINANCE => "เจ้าหน้าที่การเงิน",
            _ => "ผู้ใช้งานทั่วไป (Dashboard)",
        };

        public static void Configure(AuthorizationOptions options, RoleOptions roles)
        {
            var authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            options.AddPolicy(AUTHENTICATED, authenticated);
            options.AddPolicy(FINANCE, p => p.RequireAuthenticatedUser().RequireAssertion(ctx => IsAdmin(ctx.User, roles) || IsFinance(ctx.User, roles)));
            options.AddPolicy(ADMIN, p => p.RequireAuthenticatedUser().RequireAssertion(ctx => IsAdmin(ctx.User, roles)));

            // ทุกหน้าต้อง Login ยกเว้นที่ระบุ [AllowAnonymous]
            options.FallbackPolicy = authenticated;
        }
    }
}
