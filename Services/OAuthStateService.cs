using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataPersistentApi.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace DataPersistentApi.Services;

public class OAuthStateService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private readonly byte[] _keyBytes;

    public OAuthStateService(IOptions<JwtOptions> jwtOptions)
    {
        var key = jwtOptions.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Auth:SigningKey must be configured.");
        }

        _keyBytes = Encoding.UTF8.GetBytes(key);
    }

    public string Protect(string? clientRedirectUri, string? userState, bool expectsRedirect)
    {
        var payload = new OAuthStatePayload(clientRedirectUri, userState, expectsRedirect, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_keyBytes, jsonBytes);

        return $"{WebEncoders.Base64UrlEncode(jsonBytes)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    public bool TryUnprotect(string? protectedState, out OAuthStatePayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(protectedState))
        {
            return false;
        }

        var parts = protectedState.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var jsonBytes = WebEncoders.Base64UrlDecode(parts[0]);
            var actualSignature = WebEncoders.Base64UrlDecode(parts[1]);
            var expectedSignature = HMACSHA256.HashData(_keyBytes, jsonBytes);

            if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<OAuthStatePayload>(jsonBytes);
            if (parsed == null)
            {
                return false;
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(parsed.IssuedAtUnixSeconds);
            if (issuedAt > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - issuedAt > StateLifetime)
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public record OAuthStatePayload(string? ClientRedirectUri, string? UserState, bool ExpectsRedirect, long IssuedAtUnixSeconds);
}
