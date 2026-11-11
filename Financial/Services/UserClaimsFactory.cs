using Financial.DTOs.Account;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Financial.Services
{
    /// <summary>
    /// สร้าง ClaimsPrincipal จากข้อมูลผู้ใช้ที่ได้จาก External Auth API
    /// </summary>
    public static class UserClaimsFactory
    {
        public static ClaimsPrincipal Create(AuthData userData)
        {
            ArgumentNullException.ThrowIfNull(userData);

            var loginName = string.IsNullOrWhiteSpace(userData.LoginName) ? "Unknown" : userData.LoginName.Trim();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, loginName),
                new(ClaimTypes.Name, loginName),
                new("LoginName", loginName),
                new("FullName", ValueOrDefault(userData.FullName, loginName)),
                new("Gender", ValueOrDefault(userData.Gender)),
                new("JobPosition", ValueOrDefault(userData.JobPosition)),
                new("Division", ValueOrDefault(userData.Division)),
            };

            if (!string.IsNullOrWhiteSpace(userData.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, userData.Email.Trim()));
            }

            // เพิ่ม Roles ทั้งหมด (ตัดค่าว่างและค่าซ้ำ)
            foreach (var role in (userData.Roles ?? []).Where(r => !string.IsNullOrWhiteSpace(r))
                                                       .Select(r => r.Trim())
                                                       .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        private static string ValueOrDefault(string? value, string fallback = "-")
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
