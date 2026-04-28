using System.Security.Claims;
using DataPersistentApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Authorization;

public class ActiveRoleHandler : AuthorizationHandler<ActiveRoleRequirement>
{
    private readonly AppDBContext _db;

    public ActiveRoleHandler(AppDBContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveRoleRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive)
        {
            return;
        }

        if (requirement.AllowedRoles.Contains(user.Role, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
