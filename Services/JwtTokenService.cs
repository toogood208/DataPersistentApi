using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataPersistentApi.Configuration;
using DataPersistentApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DataPersistentApi.Services;

public class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKeyBytes;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _signingKeyBytes = Encoding.UTF8.GetBytes(GetSigningKey(_options));
    }

    public string CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("github_id", user.GitHubId),
            new("username", user.Username),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("is_active", user.IsActive ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(_signingKeyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int GetAccessTokenExpiresInSeconds() => _options.AccessTokenMinutes * 60;

    public TokenValidationParameters CreateTokenValidationParameters()
    {
        return CreateTokenValidationParameters(_options);
    }

    public static TokenValidationParameters CreateTokenValidationParameters(JwtOptions options)
    {
        var signingKeyBytes = Encoding.UTF8.GetBytes(GetSigningKey(options));

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ClockSkew = TimeSpan.Zero
        };
    }

    private static string GetSigningKey(JwtOptions options)
    {
        return string.IsNullOrWhiteSpace(options.SigningKey)
            ? "development-signing-key-change-me-before-production"
            : options.SigningKey;
    }
}
