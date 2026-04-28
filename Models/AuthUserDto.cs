namespace DataPersistentApi.Models;

public record AuthUserDto(
    string Id,
    string GitHubId,
    string Username,
    string? Email,
    string? AvatarUrl,
    string Role,
    bool IsActive);
