namespace DataPersistentApi.Endpoints;

public class CsrfEndpointFilter : IEndpointFilter
{
    private readonly string _cookieName;
    private readonly string _headerName;

    public CsrfEndpointFilter(string cookieName, string headerName)
    {
        _cookieName = cookieName;
        _headerName = headerName;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!request.Cookies.ContainsKey("insighta_access_token"))
        {
            return await next(context);
        }

        var cookieToken = request.Cookies[_cookieName];
        var headerToken = request.Headers[_headerName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(cookieToken) || !string.Equals(cookieToken, headerToken, StringComparison.Ordinal))
        {
            return Results.Json(new { status = "error", message = "CSRF validation failed" }, statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
