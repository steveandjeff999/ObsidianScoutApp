using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }
}

public class LoginResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("user")]
    public User User { get; set; } = new();
    
    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }
}

public class TokenResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }
}
