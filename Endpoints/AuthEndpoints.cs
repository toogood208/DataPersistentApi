using DataPersistentApi.Models;
using DataPersistentApi.Services;
using DataPersistentApi.Configuration;

namespace DataPersistentApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/auth")
            .RequireRateLimiting(StartupConstants.AuthRateLimitPolicy);
        var csrfFilter = new CsrfEndpointFilter(AuthCookieService.CsrfCookieName, AuthCookieService.CsrfHeaderName);

        auth.MapMethods("/{*path}", ["OPTIONS"], () => Results.Ok());

        auth.MapGet("/github", (HttpRequest request, GitHubOAuthService authService) =>
        {
            var expectsRedirect = !string.Equals(request.Query["mode"], "cli", StringComparison.OrdinalIgnoreCase);
            var start = authService.CreateAuthorizationRequest(
                request,
                request.Query["client_redirect_uri"].FirstOrDefault(),
                request.Query["state"].FirstOrDefault(),
                request.Query["code_challenge"].FirstOrDefault(),
                request.Query["code_challenge_method"].FirstOrDefault(),
                expectsRedirect);

            if (start.IsError)
            {
                return Results.BadRequest(new { status = "error", message = start.Message });
            }

            if (expectsRedirect)
            {
                return Results.Redirect(start.AuthorizeUrl!);
            }

            return Results.Ok(new
            {
                status = "success",
                data = new
                {
                    authorize_url = start.AuthorizeUrl,
                    state = start.ProtectedState
                }
            });
        });

        auth.MapGet("/github/callback", async (HttpRequest request, GitHubOAuthService authService, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("DataPersistentApi.AuthCallback");
            var code = request.Query["code"].FirstOrDefault();
            var protectedState = request.Query["state"].FirstOrDefault();
            var codeVerifier = request.Query["code_verifier"].FirstOrDefault();

            var result = await authService.CompleteOAuthAsync(request, code ?? string.Empty, protectedState, codeVerifier, ct);
            if (result.IsError)
            {
                logger.LogWarning(
                    "OAuth callback failed. Message: {Message}. HasCode: {HasCode}. HasState: {HasState}. HasCodeVerifier: {HasCodeVerifier}",
                    result.Message,
                    !string.IsNullOrWhiteSpace(code),
                    !string.IsNullOrWhiteSpace(protectedState),
                    !string.IsNullOrWhiteSpace(codeVerifier));

                if (authService.TryBuildClientErrorRedirectUrl(protectedState, result.Message ?? "OAuth callback failed", out var redirectUrl))
                {
                    return Results.Redirect(redirectUrl);
                }

                return Results.BadRequest(new { status = "error", message = result.Message });
            }

            if (result.ExpectsRedirect && !string.IsNullOrWhiteSpace(result.ClientRedirectUri))
            {
                authService.SetWebSessionCookies(request.HttpContext.Response, result.Response!.Data);
                var redirectUrl = authService.BuildClientRedirectUrl(result.ClientRedirectUri, result.UserState);
                return Results.Redirect(redirectUrl);
            }

            if (result.ExpectsRedirect)
            {
                authService.SetWebSessionCookies(request.HttpContext.Response, result.Response!.Data);
                return Results.Ok(new { status = "success", message = "Login successful" });
            }

            return Results.Ok(CreateAuthCallbackResponse(result.Response!.Data));
        });

        auth.MapPost("/refresh", async (RefreshTokenRequestDto? req, RefreshTokenService refreshTokenService, JwtTokenService jwtTokenService, AuthCookieService authCookieService, HttpRequest request, HttpResponse response, CancellationToken ct) =>
        {
            var refreshToken = req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = request.Cookies[AuthCookieService.RefreshTokenCookieName];
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Results.BadRequest(new { status = "error", message = "Missing or empty refresh_token" });
            }

            var rotated = await refreshTokenService.RotateRefreshTokenAsync(
                refreshToken,
                jwtTokenService.CreateAccessToken,
                jwtTokenService.GetAccessTokenExpiresInSeconds(),
                ct);

            if (rotated == null)
            {
                return Results.Json(new { status = "error", message = "Invalid or expired refresh token" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (request.Cookies.ContainsKey(AuthCookieService.RefreshTokenCookieName))
            {
                authCookieService.SetAuthCookies(
                    response,
                    rotated.AccessToken,
                    rotated.RefreshToken,
                    TimeSpan.FromSeconds(rotated.ExpiresIn),
                    TimeSpan.FromSeconds(rotated.RefreshExpiresIn));

                return Results.Ok(new { status = "success" });
            }

            return Results.Ok(new
            {
                status = "success",
                access_token = rotated.AccessToken,
                refresh_token = rotated.RefreshToken
            });
        }).AddEndpointFilter(csrfFilter);

        auth.MapPost("/logout", async (RefreshTokenRequestDto? req, RefreshTokenService refreshTokenService, GitHubOAuthService authService, HttpRequest request, HttpResponse response, CancellationToken ct) =>
        {
            var refreshToken = req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = request.Cookies[AuthCookieService.RefreshTokenCookieName];
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Results.BadRequest(new { status = "error", message = "Missing or empty refresh_token" });
            }

            var revoked = await refreshTokenService.RevokeRefreshTokenAsync(refreshToken, ct);
            if (!revoked)
            {
                return Results.Json(new { status = "error", message = "Invalid or expired refresh token" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            authService.ClearWebSessionCookies(response);
            return Results.Ok(new { status = "success", message = "Logged out successfully" });
        }).AddEndpointFilter(csrfFilter);
    }

    private static AuthCallbackResponseDto CreateAuthCallbackResponse(AuthTokenPairDto tokenPair)
    {
        return new AuthCallbackResponseDto(
            "success",
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.TokenType,
            tokenPair.ExpiresIn,
            tokenPair.RefreshExpiresIn,
            tokenPair.User);
    }
}
