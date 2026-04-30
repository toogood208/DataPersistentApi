using System.Text.Json.Serialization;

namespace DataPersistentApi.Models;

public record AuthCallbackResponseDto(
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("refresh_token")]
    string RefreshToken,
    [property: JsonPropertyName("token_type")]
    string TokenType,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonPropertyName("refresh_expires_in")]
    int RefreshExpiresIn,
    [property: JsonPropertyName("user")]
    AuthUserDto User);
