using System.Text.Json.Serialization;

namespace Financial.DTOs.Account
{
    /// <summary>
    /// Response จาก External Auth API
    /// </summary>
    public class AuthResponse
    {
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public AuthData? Data { get; set; }
    }

    public class AuthData
    {
        [JsonPropertyName("loginName")]
        public string LoginName { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonPropertyName("jobPosition")]
        public string JobPosition { get; set; } = string.Empty;

        [JsonPropertyName("division")]
        public string Division { get; set; } = string.Empty;

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = new();

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }
}
