using System.Text.Json.Serialization;

namespace DataPersistentApi.Models;

public record RefreshTokenRequestDto([property: JsonPropertyName("refresh_token")] string? RefreshToken);
