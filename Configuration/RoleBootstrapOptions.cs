namespace DataPersistentApi.Configuration;

public sealed class RoleBootstrapOptions
{
    public const string SectionName = "RoleBootstrap";

    public string[] AdminUsernames { get; set; } = [];
    public string[] AdminEmails { get; set; } = [];
}
