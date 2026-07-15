using Financial.DTOs.Account;
using Financial.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Financial.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<ILogger<AuthService>> _loggerMock;
        private readonly Mock<IOptions<AuthSettings>> _optionsMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _loggerMock = new Mock<ILogger<AuthService>>();
            _optionsMock = new Mock<IOptions<AuthSettings>>();

            // Setup AuthSettings
            var authSettings = new AuthSettings
            {
                AuthEndpoint = "http://10.67.67.166/QSHCAuth/api/Account/HOAuthJson",
                TimeoutSeconds = 30
            };
            _optionsMock.Setup(x => x.Value).Returns(authSettings);
        }

        [Fact]
        public async Task AuthenticateAsync_ValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var username = "it099";
            var password = "abc12345";
            var expectedResponse = new AuthResponse
            {
                IsSuccess = true,
                Status = "1",
                Message = "Success",
                Data = new AuthData
                {
                    LoginName = "it099",
                    FullName = "ทดสอบ ระบบ",
                    Gender = "ชาย",
                    JobPosition = "นักวิชาการคอมพิวเตอร์",
                    Division = "ฝ่ายเทคโนโลยีสารสนเทศ",
                    Email = "it099@hospital.go.th",
                    Roles = new List<string> { "Admin" }
                }
            };

            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            var authService = new AuthService(httpClient, _loggerMock.Object, _optionsMock.Object);

            // Act
            var result = await authService.AuthenticateAsync(username, password);

            // Assert
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Status.Should().Be("1");
            result.Message.Should().Be("Success");
            result.Data.Should().NotBeNull();
            result.Data!.LoginName.Should().Be("it099");
            result.Data.FullName.Should().Be("ทดสอบ ระบบ");
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidCredentials_ReturnsNull()
        {
            // Arrange
            var username = "invalid";
            var password = "wrong";

            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            var authService = new AuthService(httpClient, _loggerMock.Object, _optionsMock.Object);

            // Act
            var result = await authService.AuthenticateAsync(username, password);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task AuthenticateAsync_EmptyUsername_ReturnsNull()
        {
            // Arrange
            var username = "";
            var password = "password";

            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            var authService = new AuthService(httpClient, _loggerMock.Object, _optionsMock.Object);

            // Act
            var result = await authService.AuthenticateAsync(username, password);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task AuthenticateAsync_EmptyPassword_ReturnsNull()
        {
            // Arrange
            var username = "testuser";
            var password = "";

            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            var authService = new AuthService(httpClient, _loggerMock.Object, _optionsMock.Object);

            // Act
            var result = await authService.AuthenticateAsync(username, password);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task AuthenticateAsync_HttpException_ReturnsNull()
        {
            // Arrange
            var username = "testuser";
            var password = "password";

            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            var authService = new AuthService(httpClient, _loggerMock.Object, _optionsMock.Object);

            // Act
            var result = await authService.AuthenticateAsync(username, password);

            // Assert
            result.Should().BeNull();
        }
    }
}
