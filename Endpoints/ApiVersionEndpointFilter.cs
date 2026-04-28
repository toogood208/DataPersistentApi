namespace DataPersistentApi.Endpoints;

public class ApiVersionEndpointFilter : IEndpointFilter
{
    private readonly string _headerName;
    private readonly string _requiredValue;

    public ApiVersionEndpointFilter(string headerName, string requiredValue)
    {
        _headerName = headerName;
        _requiredValue = requiredValue;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var headerValue = request.Headers[_headerName].FirstOrDefault();

        if (!string.Equals(headerValue, _requiredValue, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { status = "error", message = "API version header required" });
        }

        return await next(context);
    }
}
