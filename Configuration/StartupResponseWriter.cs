using DataPersistentApi.Models;

namespace DataPersistentApi.Configuration;

public static class StartupResponseWriter
{
    public static Task WriteErrorResponseAsync(HttpContext httpContext, int statusCode, string message, CancellationToken cancellationToken = default)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsJsonAsync(new ErrorResponseDto("error", message), cancellationToken: cancellationToken);
    }
}
