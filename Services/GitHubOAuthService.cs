using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DataPersistentApi.Configuration;
using DataPersistentApi.Data;
using DataPersistentApi.Models;
using DataPersistentApi.Utils;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataPersistentApi.Services;

public class GitHubOAuthService
{
    private readonly AppDBContext _db;
    private readonly IOptionsMonitor<GitHubOAuthOptions> _gitHubOptionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthStateService _stateService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly AuthCookieService _authCookieService;
    private readonly RoleBootstrapOptions _roleBootstrapOptions;
    private readonly ILogger<GitHubOAuthService> _logger;

    public GitHubOAuthService(
        AppDBContext db,
        IHttpClientFactory httpClientFactory,
        OAuthStateService stateService,
        JwtTokenService jwtTokenService,
        RefreshTokenService refreshTokenService,
        AuthCookieService authCookieService,
        ILogger<GitHubOAuthService> logger,
        IOptionsMonitor<GitHubOAuthOptions> gitHubOptionsMonitor,
        IOptions<RoleBootstrapOptions> roleBootstrapOptions)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _stateService = stateService;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _authCookieService = authCookieService;
        _logger = logger;
        _gitHubOptionsMonitor = gitHubOptionsMonitor;
        _roleBootstrapOptions = roleBootstrapOptions.Value;
    }

    public AuthRedirectStartResult CreateAuthorizationRequest(
        HttpRequest request,
        string? clientRedirectUri,
        string? userState,
        string? codeChallenge,
        string? codeChallengeMethod,
        bool expectsRedirect)
    {
        var options = ResolveOptions(expectsRedirect);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            return AuthRedirectStartResult.Failure("GitHub OAuth is not configured");
        }

        if (!string.IsNullOrWhiteSpace(codeChallenge) && !string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return AuthRedirectStartResult.Failure("PKCE requires code_challenge_method=S256");
        }

        if (string.IsNullOrWhiteSpace(codeChallenge) && !string.IsNullOrWhiteSpace(codeChallengeMethod))
        {
            return AuthRedirectStartResult.Failure("code_challenge is required when code_challenge_method is provided");
        }

        if (!expectsRedirect && string.IsNullOrWhiteSpace(codeChallenge))
        {
            return AuthRedirectStartResult.Failure("CLI OAuth requires PKCE");
        }

        if (!expectsRedirect && string.IsNullOrWhiteSpace(clientRedirectUri))
        {
            return AuthRedirectStartResult.Failure("CLI OAuth requires client_redirect_uri");
        }

        if (!string.IsNullOrWhiteSpace(clientRedirectUri) && !Uri.TryCreate(clientRedirectUri, UriKind.Absolute, out _))
        {
            return AuthRedirectStartResult.Failure("Invalid client_redirect_uri");
        }

        var protectedState = _stateService.Protect(clientRedirectUri, userState, expectsRedirect);
        var redirectUri = ResolveAuthorizationRedirectUri(request, clientRedirectUri, expectsRedirect);
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = options.Scope,
            ["state"] = protectedState
        };

        if (!string.IsNullOrWhiteSpace(codeChallenge))
        {
            query["code_challenge"] = codeChallenge;
            query["code_challenge_method"] = codeChallengeMethod;
        }

        var authorizeUrl = QueryHelpers.AddQueryString(options.AuthorizeUrl, query);
        return AuthRedirectStartResult.Success(authorizeUrl, protectedState);
    }

    public async Task<AuthCompletionResult> CompleteOAuthAsync(HttpRequest request, string code, string? protectedState, string? codeVerifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("OAuth callback failed because the code parameter was missing or empty.");
            return AuthCompletionResult.Failure("Missing or empty code");
        }

        if (string.IsNullOrWhiteSpace(protectedState))
        {
            _logger.LogWarning("OAuth callback failed because the state parameter was missing or empty.");
            return AuthCompletionResult.Failure("Invalid OAuth state");
        }

        if (!_stateService.TryUnprotect(protectedState, out var statePayload))
        {
            _logger.LogWarning("OAuth callback failed because the protected state could not be validated.");
            return AuthCompletionResult.Failure("Invalid OAuth state");
        }

        var options = ResolveOptions(statePayload?.ExpectsRedirect ?? true);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            _logger.LogWarning("OAuth callback failed because GitHub OAuth is not configured for {Mode}.", statePayload?.ExpectsRedirect ?? true ? "web" : "cli");
            return AuthCompletionResult.Failure("GitHub OAuth is not configured");
        }

        var redirectUri = ResolveTokenExchangeRedirectUri(request, statePayload);

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InsightaBackend", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(BuildTokenExchangeForm(options, code, codeVerifier, redirectUri))
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await client.SendAsync(tokenRequest, ct);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("OAuth callback failed during GitHub token exchange request.");
            return AuthCompletionResult.Failure("GitHub token exchange failed");
        }

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("OAuth callback failed because GitHub token exchange returned status code {StatusCode}.", (int)tokenResponse.StatusCode);
            return AuthCompletionResult.Failure("GitHub token exchange failed");
        }

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<GitHubAccessTokenResponse>(cancellationToken: ct);
        if (tokenPayload == null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            _logger.LogWarning("OAuth callback failed because GitHub did not return a usable access token payload.");
            return AuthCompletionResult.Failure("GitHub token exchange failed");
        }

        var profile = await GetGitHubProfileAsync(client, options, tokenPayload.AccessToken, ct);
        if (profile == null || profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Login))
        {
            _logger.LogWarning("OAuth callback failed because GitHub user lookup did not return a valid profile.");
            return AuthCompletionResult.Failure("GitHub user lookup failed");
        }

        var email = profile.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = await GetPrimaryEmailAsync(client, options, tokenPayload.AccessToken, ct);
        }

        _logger.LogInformation(
            "GitHub OAuth callback resolved user {Username} with email present: {HasEmail}. ExpectsRedirect: {ExpectsRedirect}.",
            profile.Login,
            !string.IsNullOrWhiteSpace(email),
            statePayload?.ExpectsRedirect ?? false);

        var user = await UpsertUserAsync(profile, email, ct);
        var accessToken = _jwtTokenService.CreateAccessToken(user);
        var tokenPair = await _refreshTokenService.IssueTokenPairAsync(
            user,
            accessToken,
            _jwtTokenService.GetAccessTokenExpiresInSeconds(),
            ct);

        return AuthCompletionResult.Success(
            new AuthSuccessResponseDto("success", tokenPair),
            statePayload?.ClientRedirectUri,
            statePayload?.UserState,
            statePayload?.ExpectsRedirect ?? false);
    }

    public string BuildClientRedirectUrl(string clientRedirectUri, string? userState)
    {
        var fragmentValues = new Dictionary<string, string?>
        {
            ["login"] = "success",
            ["state"] = userState
        };

        var fragment = string.Join(
            "&",
            fragmentValues
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        var builder = new StringBuilder(clientRedirectUri);
        builder.Append(clientRedirectUri.Contains('#') ? "&" : "#");
        builder.Append(fragment);
        return builder.ToString();
    }

    public string BuildClientErrorRedirectUrl(string clientRedirectUri, string message, string? userState)
    {
        var fragmentValues = new Dictionary<string, string?>
        {
            ["error"] = message,
            ["state"] = userState
        };

        var fragment = string.Join(
            "&",
            fragmentValues
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return $"{clientRedirectUri}#{fragment}";
    }

    public bool TryBuildClientErrorRedirectUrl(string? protectedState, string message, out string redirectUrl)
    {
        redirectUrl = string.Empty;
        if (!_stateService.TryUnprotect(protectedState, out var payload) ||
            string.IsNullOrWhiteSpace(payload.ClientRedirectUri) ||
            !payload.ExpectsRedirect)
        {
            return false;
        }

        redirectUrl = BuildClientErrorRedirectUrl(payload.ClientRedirectUri, message, payload.UserState);
        return true;
    }

    public void SetWebSessionCookies(HttpResponse response, AuthTokenPairDto tokenPair)
    {
        _authCookieService.SetAuthCookies(
            response,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            TimeSpan.FromSeconds(tokenPair.ExpiresIn),
            TimeSpan.FromSeconds(tokenPair.RefreshExpiresIn));
    }

    public void ClearWebSessionCookies(HttpResponse response)
    {
        _authCookieService.ClearAuthCookies(response);
    }

    private string ResolveBackendCallbackUrl(HttpRequest request)
    {
        var options = ResolveOptions(true);
        if (!string.IsNullOrWhiteSpace(options.CallbackUrl))
        {
            return options.CallbackUrl;
        }

        return $"{request.Scheme}://{request.Host}/auth/github/callback";
    }

    private string ResolveAuthorizationRedirectUri(HttpRequest request, string? clientRedirectUri, bool expectsRedirect)
    {
        if (!expectsRedirect && !string.IsNullOrWhiteSpace(clientRedirectUri))
        {
            return clientRedirectUri;
        }

        return ResolveBackendCallbackUrl(request);
    }

    private string ResolveTokenExchangeRedirectUri(HttpRequest request, OAuthStateService.OAuthStatePayload? statePayload)
    {
        if (statePayload is { ExpectsRedirect: false } &&
            !string.IsNullOrWhiteSpace(statePayload.ClientRedirectUri))
        {
            return statePayload.ClientRedirectUri;
        }

        return ResolveBackendCallbackUrl(request);
    }

    private Dictionary<string, string> BuildTokenExchangeForm(GitHubOAuthOptions options, string code, string? codeVerifier, string redirectUri)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            form["code_verifier"] = codeVerifier;
        }

        return form;
    }

    private async Task<GitHubUserProfileResponse?> GetGitHubProfileAsync(HttpClient client, GitHubOAuthOptions options, string accessToken, CancellationToken ct)
    {
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, options.UserUrl);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await client.SendAsync(profileRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GitHubUserProfileResponse>(cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<string?> GetPrimaryEmailAsync(HttpClient client, GitHubOAuthOptions options, string accessToken, CancellationToken ct)
    {
        using var emailRequest = new HttpRequestMessage(HttpMethod.Get, options.EmailsUrl);
        emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await client.SendAsync(emailRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var emails = await response.Content.ReadFromJsonAsync<List<GitHubUserEmailResponse>>(cancellationToken: ct);
            return emails?
                .Where(e => e.Verified)
                .OrderByDescending(e => e.Primary)
                .Select(e => e.Email)
                .FirstOrDefault();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<User> UpsertUserAsync(GitHubUserProfileResponse profile, string? email, CancellationToken ct)
    {
        var gitHubId = profile.Id.ToString();
        var username = profile.Login.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        var avatarUrl = string.IsNullOrWhiteSpace(profile.AvatarUrl) ? null : profile.AvatarUrl.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubId, ct);
        if (user == null)
        {
            user = new User
            {
                Id = UuidV7Generator.NewUuidV7(),
                GitHubId = gitHubId,
                Username = username,
                Email = normalizedEmail,
                AvatarUrl = avatarUrl,
                Role = ResolveInitialRole(username, normalizedEmail),
                IsActive = true,
                LastLoginAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Username = username;
            user.Email = normalizedEmail;
            user.AvatarUrl = avatarUrl;
            user.LastLoginAt = DateTime.UtcNow;
            if (ShouldBootstrapAdmin(username, normalizedEmail))
            {
                user.Role = "admin";
            }
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    private string ResolveInitialRole(string username, string? email)
    {
        return ShouldBootstrapAdmin(username, email) ? "admin" : "analyst";
    }

    private bool ShouldBootstrapAdmin(string username, string? email)
    {
        if (_roleBootstrapOptions.AdminUsernames.Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(email) &&
               _roleBootstrapOptions.AdminEmails.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
    }

    private GitHubOAuthOptions ResolveOptions(bool expectsRedirect)
    {
        var optionName = expectsRedirect
            ? GitHubOAuthOptions.WebOptionsName
            : GitHubOAuthOptions.CliOptionsName;

        var options = _gitHubOptionsMonitor.Get(optionName);
        if (!expectsRedirect && string.IsNullOrWhiteSpace(options.ClientId))
        {
            return _gitHubOptionsMonitor.Get(GitHubOAuthOptions.WebOptionsName);
        }

        return options;
    }

    public record AuthRedirectStartResult(bool IsError, string? Message, string? AuthorizeUrl, string? ProtectedState)
    {
        public static AuthRedirectStartResult Failure(string message) => new(true, message, null, null);
        public static AuthRedirectStartResult Success(string authorizeUrl, string protectedState) => new(false, null, authorizeUrl, protectedState);
    }

    public record AuthCompletionResult(
        bool IsError,
        string? Message,
        AuthSuccessResponseDto? Response,
        string? ClientRedirectUri,
        string? UserState,
        bool ExpectsRedirect)
    {
        public static AuthCompletionResult Failure(string message) => new(true, message, null, null, null, false);

        public static AuthCompletionResult Success(
            AuthSuccessResponseDto response,
            string? clientRedirectUri,
            string? userState,
            bool expectsRedirect) =>
            new(false, null, response, clientRedirectUri, userState, expectsRedirect);
    }
}
