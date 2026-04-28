using Microsoft.AspNetCore.Authorization;

namespace DataPersistentApi.Authorization;

public class ActiveRoleRequirement : IAuthorizationRequirement
{
    public ActiveRoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }

    public IReadOnlyCollection<string> AllowedRoles { get; }
}
