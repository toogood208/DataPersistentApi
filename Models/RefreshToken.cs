namespace DataPersistentApi.Models;

public class RefreshToken
{
    public string Id { get; set; } = null!;                 // UUID v7
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }                 // UTC
    public DateTime CreatedAt { get; set; }                 // UTC
    public DateTime? RevokedAt { get; set; }                // UTC
    public string? ReplacedByTokenHash { get; set; }

    public User User { get; set; } = null!;
}
