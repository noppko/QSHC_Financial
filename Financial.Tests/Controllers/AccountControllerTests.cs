using Financial.Controllers;
using Financial.DTOs.Account;
using Financial.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace Financial.Tests.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<IAuthService> _authService = new();
        private readonly Mock<IAuthenticationService> _authenticationService = new();

        private AccountController CreateController(ClaimsPrincipal? user = null)
        {
            var services = new ServiceCollection()
                .AddSingleton(_authenticationService.Object)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
            };

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
                     .Returns<string>(url => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\"));

            return new AccountController(_authService.Object, NullLogger<AccountController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                Url = urlHelper.Object,
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            };
        }

        private void SetupApi(AuthResponse? response) =>
            _authService.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(response);

        private static AuthResponse Success() => new()
        {
            IsSuccess = true,
            Data = new AuthData { LoginName = "it099", FullName = "ทดสอบ ระบบ", Roles = ["Finance"] },
        };

        [Fact]
        public async Task Login_ValidCredentials_SignsInAndRedirectsHome()
        {
            SetupApi(Success());
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = " it099 ", Password = "secret" }, CancellationToken.None);

            result.Should().BeOfType<RedirectToActionResult>().Which.ControllerName.Should().Be("Home");
            _authService.Verify(s => s.AuthenticateAsync("it099", "secret", It.IsAny<CancellationToken>()), Times.Once, "ควรตัดช่องว่างหน้า/หลังชื่อผู้ใช้");
            _authenticationService.Verify(a => a.SignInAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.Is<ClaimsPrincipal>(p => p.Identity!.Name == "it099" && p.IsInRole("Finance")),
                It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        [Fact]
        public async Task Login_LocalReturnUrl_RedirectsBack()
        {
            SetupApi(Success());
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = "it099", Password = "x", ReturnUrl = "/Discharges/IPD?start=2026-01-01" }, CancellationToken.None);

            result.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/Discharges/IPD?start=2026-01-01");
        }

        [Theory]
        [InlineData("https://evil.example.com/")]
        [InlineData("//evil.example.com")]
        [InlineData("/\\evil.example.com")]
        public async Task Login_ExternalReturnUrl_IsIgnored(string returnUrl)
        {
            SetupApi(Success());
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = "it099", Password = "x", ReturnUrl = returnUrl }, CancellationToken.None);

            result.Should().BeOfType<RedirectToActionResult>("ป้องกัน Open Redirect").Which.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task Login_WrongPassword_ShowsErrorAndClearsPassword()
        {
            SetupApi(new AuthResponse { IsSuccess = false });
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = "it099", Password = "wrong" }, CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.Model.As<LoginRequest>().Password.Should().BeEmpty();
            controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage.Should().Be("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
            _authenticationService.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        }

        [Fact]
        public async Task Login_AuthApiUnavailable_ShowsServiceError()
        {
            SetupApi(null);
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = "it099", Password = "x" }, CancellationToken.None);

            result.Should().BeOfType<ViewResult>();
            controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage.Should().Contain("ไม่สามารถเชื่อมต่อระบบยืนยันตัวตน");
        }

        [Fact]
        public async Task Login_EmptyFields_DoesNotCallApi()
        {
            var controller = CreateController();

            var result = await controller.Login(new LoginRequest { Username = " ", Password = "" }, CancellationToken.None);

            result.Should().BeOfType<ViewResult>();
            _authService.VerifyNoOtherCalls();
        }

        [Fact]
        public void LoginGet_AlreadyAuthenticated_RedirectsToReturnUrl()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "it099")], "Cookies"));
            var controller = CreateController(user);

            var result = controller.Login("/Home/Privacy");

            result.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/Home/Privacy");
        }

        [Fact]
        public void LoginGet_Anonymous_KeepsReturnUrlInModel()
        {
            var controller = CreateController();

            var result = controller.Login("/Discharges/OPD");

            result.Should().BeOfType<ViewResult>().Which.Model.As<LoginRequest>().ReturnUrl.Should().Be("/Discharges/OPD");
        }

        [Fact]
        public async Task Logout_SignsOutAndRedirectsToLogin()
        {
            var controller = CreateController();

            var result = await controller.Logout();

            result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Login");
            _authenticationService.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<AuthenticationProperties>()), Times.Once);
        }
    }
}
