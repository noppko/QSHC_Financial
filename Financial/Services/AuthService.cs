using Financial.DTOs.Account;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Financial.Services
{
    /// <summary>
    /// Service สำหรับ Authentication ผ่าน External API
    /// </summary>
    public class AuthService : IAuthService
    {
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
        public async Task<AuthResponse?> AuthenticateAsync(string username, string password)
        {
            try
            {
                // ห้ามเก็บ/log password
                _logger.LogInformation("Attempting authentication for user: {Username}", username);

                var requestBody = new
                {
                    username = username,
                    password = password
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_settings.AuthEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Authentication failed with status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during authentication for user: {Username}", username);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication for user: {Username}", username);
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
