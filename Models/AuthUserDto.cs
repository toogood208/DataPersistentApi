using System.Text.Json.Serialization;

namespace DataPersistentApi.Models;

public record AuthUserDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("github_id")]
    string GitHubId,
    [property: JsonPropertyName("username")]
    string Username,
    [property: JsonPropertyName("email")]
    string? Email,
    [property: JsonPropertyName("avatar_url")]
    string? AvatarUrl,
    [property: JsonPropertyName("role")]
    string Role,
    [property: JsonPropertyName("is_active")]
    bool IsActive);
