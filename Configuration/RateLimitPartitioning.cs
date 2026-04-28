using System.Security.Claims;

namespace DataPersistentApi.Configuration;

public static class RateLimitPartitioning
{
    public static string GetClientPartitionKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return $"client:{forwardedFor}";
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp) ? "client:unknown" : $"client:{remoteIp}";
    }

    public static string GetApiPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        return GetClientPartitionKey(httpContext);
    }
}
