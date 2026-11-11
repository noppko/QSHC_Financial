using Financial.DTOs.Account;
using Financial.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการ Authentication (Login, Logout)
    /// </summary>
    [AllowAnonymous]
    public class AccountController : Controller
    {
        public const string LOGIN_RATE_LIMIT_POLICY = "login";

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
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new LoginRequest { ReturnUrl = returnUrl });
        }

        /// <summary>
        /// ประมวลผล Login ผ่าน External Auth API
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(LOGIN_RATE_LIMIT_POLICY)]
        public async Task<IActionResult> Login(LoginRequest login, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                ModelState.AddModelError(string.Empty, "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน");
                return View(ClearPassword(login));
            }

            var authResponse = await _authService.AuthenticateAsync(login.Username.Trim(), login.Password, cancellationToken);

            if (authResponse is null)
            {
                ModelState.AddModelError(string.Empty, "ไม่สามารถเชื่อมต่อระบบยืนยันตัวตนได้ กรุณาลองใหม่อีกครั้ง");
                return View(ClearPassword(login));
            }

            if (!authResponse.IsSuccess || authResponse.Data is null)
            {
                _logger.LogWarning("Login failed for user: {Username}", login.Username);
                ModelState.AddModelError(string.Empty, "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
                return View(ClearPassword(login));
            }

            var principal = UserClaimsFactory.Create(authResponse.Data);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = login.RememberMe,
                AllowRefresh = true,
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            _logger.LogInformation("User {Username} logged in successfully", principal.Identity?.Name);

            return RedirectToLocal(login.ReturnUrl);
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

            return RedirectToAction(nameof(Login));
        }

        /// <summary>
        /// หน้า Access Denied
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        /// <summary>
        /// Redirect เฉพาะ URL ภายในระบบเท่านั้น (ป้องกัน Open Redirect)
        /// </summary>
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private static LoginRequest ClearPassword(LoginRequest login)
        {
            login.Password = string.Empty;
            return login;
        }
    }
}
