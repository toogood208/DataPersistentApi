namespace DataPersistentApi.Configuration;

public sealed class GitHubOAuthOptions
{
    public const string SectionName = "GitHub";
    public const string CliSectionName = "GitHubCli";
    public const string WebOptionsName = "GitHubWeb";
    public const string CliOptionsName = "GitHubCli";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = "read:user user:email";
    public string AuthorizeUrl { get; set; } = "https://github.com/login/oauth/authorize";
    public string TokenUrl { get; set; } = "https://github.com/login/oauth/access_token";
    public string UserUrl { get; set; } = "https://api.github.com/user";
    public string EmailsUrl { get; set; } = "https://api.github.com/user/emails";
}
