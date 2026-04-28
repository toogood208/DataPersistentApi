namespace DataPersistentApi.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 3;
    public int RefreshTokenMinutes { get; set; } = 5;
}
