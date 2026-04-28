using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace DataPersistentApi.Services;

public class AuthCookieService
{
    public const string AccessTokenCookieName = "insighta_access_token";
    public const string RefreshTokenCookieName = "insighta_refresh_token";
    public const string CsrfCookieName = "insighta_csrf";
    public const string CsrfHeaderName = "X-CSRF-Token";

    public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken, TimeSpan accessLifetime, TimeSpan refreshLifetime)
    {
        response.Cookies.Append(AccessTokenCookieName, accessToken, CreateCookieOptions(accessLifetime, httpOnly: true));
        response.Cookies.Append(RefreshTokenCookieName, refreshToken, CreateCookieOptions(refreshLifetime, httpOnly: true));
        response.Cookies.Append(CsrfCookieName, CreateCsrfToken(), CreateCookieOptions(refreshLifetime, httpOnly: false));
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenCookieName, CreateCookieOptions(TimeSpan.Zero, httpOnly: true));
        response.Cookies.Delete(RefreshTokenCookieName, CreateCookieOptions(TimeSpan.Zero, httpOnly: true));
        response.Cookies.Delete(CsrfCookieName, CreateCookieOptions(TimeSpan.Zero, httpOnly: false));
    }

    private static CookieOptions CreateCookieOptions(TimeSpan lifetime, bool httpOnly)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.Add(lifetime),
            IsEssential = true
        };
    }

    private static string CreateCsrfToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }
}
