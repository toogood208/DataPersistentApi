using System.Security.Claims;
using DataPersistentApi.Configuration;
using DataPersistentApi.Data;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users")
            .RequireAuthorization(StartupConstants.ReadAccessPolicy)
            .RequireRateLimiting(StartupConstants.ApiRateLimitPolicy);

        users.MapGet("/me", async (ClaimsPrincipal principal, AppDBContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Json(new { status = "error", message = "Authentication is required" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                return Results.NotFound(new { status = "error", message = "User not found" });
            }

            return Results.Ok(new
            {
                status = "success",
                data = new
                {
                    id = user.Id,
                    github_id = user.GitHubId,
                    username = user.Username,
                    email = user.Email,
                    avatar_url = user.AvatarUrl,
                    role = user.Role,
                    is_active = user.IsActive,
                    last_login_at = user.LastLoginAt.ToString("o"),
                    created_at = user.CreatedAt.ToString("o")
                }
            });
        });
    }
}
