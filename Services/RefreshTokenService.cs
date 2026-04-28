using System.Security.Cryptography;
using System.Text;
using DataPersistentApi.Configuration;
using DataPersistentApi.Data;
using DataPersistentApi.Models;
using DataPersistentApi.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataPersistentApi.Services;

public class RefreshTokenService
{
    private readonly AppDBContext _db;
    private readonly JwtOptions _options;

    public RefreshTokenService(AppDBContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<AuthTokenPairDto> IssueTokenPairAsync(User user, string accessToken, int accessTokenExpiresInSeconds, CancellationToken ct = default)
    {
        var refreshToken = GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.RefreshTokenMinutes);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = UuidV7Generator.NewUuidV7(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        return CreateTokenPairDto(user, accessToken, refreshToken, accessTokenExpiresInSeconds);
    }

    public async Task<AuthTokenPairDto?> RotateRefreshTokenAsync(string refreshToken, Func<User, string> createAccessToken, int accessTokenExpiresInSeconds, CancellationToken ct = default)
    {
        var existing = await GetActiveTokenEntityAsync(refreshToken, ct);
        if (existing == null)
        {
            return null;
        }

        existing.RevokedAt = DateTime.UtcNow;

        var replacementToken = GenerateRefreshToken();
        var replacementTokenHash = HashToken(replacementToken);
        existing.ReplacedByTokenHash = replacementTokenHash;

        var replacement = new RefreshToken
        {
            Id = UuidV7Generator.NewUuidV7(),
            UserId = existing.UserId,
            TokenHash = replacementTokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.RefreshTokenMinutes),
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        var accessToken = createAccessToken(existing.User);
        return CreateTokenPairDto(existing.User, accessToken, replacementToken, accessTokenExpiresInSeconds);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await GetActiveTokenEntityAsync(refreshToken, ct);
        if (existing == null)
        {
            return false;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<User?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await GetActiveTokenEntityAsync(refreshToken, ct);
        return existing?.User;
    }

    private async Task<RefreshToken?> GetActiveTokenEntityAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshTokenHash = HashToken(refreshToken);

        return await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(
                rt => rt.TokenHash == refreshTokenHash &&
                      rt.RevokedAt == null &&
                      rt.ExpiresAt > DateTime.UtcNow,
                ct);
    }

    private AuthTokenPairDto CreateTokenPairDto(User user, string accessToken, string refreshToken, int accessTokenExpiresInSeconds)
    {
        return new AuthTokenPairDto(
            accessToken,
            refreshToken,
            "Bearer",
            accessTokenExpiresInSeconds,
            _options.RefreshTokenMinutes * 60,
            new AuthUserDto(
                user.Id,
                user.GitHubId,
                user.Username,
                user.Email,
                user.AvatarUrl,
                user.Role,
                user.IsActive));
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
