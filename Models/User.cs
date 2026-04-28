namespace DataPersistentApi.Models;

public class User
{
    public string Id { get; set; } = null!;                 // UUID v7
    public string GitHubId { get; set; } = null!;           // GitHub user id
    public string Username { get; set; } = null!;           // GitHub username
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "analyst";           // analyst|admin
    public bool IsActive { get; set; } = true;
    public DateTime LastLoginAt { get; set; }               // UTC
    public DateTime CreatedAt { get; set; }                 // UTC

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
