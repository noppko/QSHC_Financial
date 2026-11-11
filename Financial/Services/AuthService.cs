using Financial.DTOs.Account;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Financial.Services
{
    /// <summary>
    /// Service สำหรับ Authentication ผ่าน External API
    /// </summary>
    public class AuthService : IAuthService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthService> _logger;
        private readonly AuthSettings _settings;

        public AuthService(
            HttpClient httpClient,
            ILogger<AuthService> logger,
            IOptions<AuthSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;
        }

        /// <summary>
        /// ทำการ Login ผ่าน External Auth API
        /// </summary>
        /// <returns>
        /// AuthResponse เมื่อ API ตอบกลับ (IsSuccess=false หมายถึงชื่อผู้ใช้/รหัสผ่านไม่ถูกต้อง),
        /// null เมื่อเชื่อมต่อ API ไม่ได้หรือ API ผิดพลาด
        /// </returns>
        public async Task<AuthResponse?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return new AuthResponse { IsSuccess = false, Message = "Username and password are required" };
            }

            try
            {
                // ห้ามเก็บ/log password
                _logger.LogInformation("Attempting authentication for user: {Username}", username);

                // AuthEndpoint เป็น absolute URL จาก appsettings.json (ไม่ต้องใช้ BaseAddress)
                using var response = await _httpClient.PostAsJsonAsync(
                    _settings.AuthEndpoint,
                    new { username, password },
                    cancellationToken);

                // API บางเวอร์ชันตอบ 400/401/403 เมื่อรหัสผ่านผิด — ถือเป็น "login ไม่สำเร็จ" ไม่ใช่ "ระบบล่ม"
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Authentication rejected for user: {Username} ({StatusCode})", username, (int)response.StatusCode);
                    return new AuthResponse { IsSuccess = false, Status = ((int)response.StatusCode).ToString(), Message = "Invalid credentials" };
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Authentication failed with status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken);

                if (authResponse?.IsSuccess == true)
                {
                    _logger.LogInformation("Authentication successful for user: {Username}", username);
                }
                else
                {
                    _logger.LogWarning("Authentication failed for user: {Username}, Message: {Message}",
                        username, authResponse?.Message ?? "Unknown error");
                }

                return authResponse;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or TimeoutException
                                          or Polly.CircuitBreaker.BrokenCircuitException or Polly.Timeout.TimeoutRejectedException)
            {
                _logger.LogError(ex, "Auth API unavailable for user: {Username}", username);
                return null;
            }
        }
    }

    /// <summary>
    /// การตั้งค่า Authentication จาก appsettings.json
    /// </summary>
    public class AuthSettings
    {
        public string AuthEndpoint { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
