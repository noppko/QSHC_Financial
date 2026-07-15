using Financial.DTOs.Account;
using Financial.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการ Authentication (Login, Logout)
    /// </summary>
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAuthService authService,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// แสดงหน้า Login
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            var model = new LoginRequest
            {
                ReturnUrl = returnUrl
            };
            return View(model);
        }

        /// <summary>
        /// ประมวลผล Login ผ่าน External Auth API
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequest login)
        {
            if (!ModelState.IsValid)
            {
                return View(login);
            }

            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                ModelState.AddModelError(string.Empty, "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน");
                return View(login);
            }

            try
            {
                // เรียก External Auth API
                var authResponse = await _authService.AuthenticateAsync(login.Username, login.Password);

                if (authResponse?.IsSuccess == true && authResponse.Data != null)
                {
                    var userData = authResponse.Data;

                    // สร้าง Claims สำหรับ Cookie Authentication
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, userData.LoginName),
                        new Claim("LoginName", userData.LoginName),
                        new Claim("FullName", userData.FullName),
                        new Claim("Gender", userData.Gender),
                        new Claim("JobPosition", userData.JobPosition),
                        new Claim("Division", userData.Division),
                        new Claim(ClaimTypes.Email, userData.Email)
                    };

                    // เพิ่ม Roles ทั้งหมด
                    foreach (var role in userData.Roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = login.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("User {Username} logged in successfully", userData.LoginName);

                    // Redirect ไปยังหน้าที่ต้องการหรือหน้า Home
                    if (!string.IsNullOrEmpty(login.ReturnUrl) && Url.IsLocalUrl(login.ReturnUrl))
                    {
                        return Redirect(login.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    _logger.LogWarning("Login failed for user: {Username}", login.Username);
                    ModelState.AddModelError(string.Empty, "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
                    return View(login);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login process for user: {Username}", login.Username);
                ModelState.AddModelError(string.Empty, "เกิดข้อผิดพลาดในการเข้าสู่ระบบ กรุณาลองใหม่อีกครั้ง");
                return View(login);
            }
        }

        /// <summary>
        /// Logout ออกจากระบบ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("User {Username} logged out", username ?? "Unknown");

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// หน้า Access Denied
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
