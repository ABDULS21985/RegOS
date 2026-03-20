namespace FC.Engine.Api.Middleware;

public class RequestIdMiddleware
{
    private const string RequestIdHeader = "X-Request-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[RequestIdHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128)
        {
            requestId = Guid.NewGuid().ToString("N")[..16];
        }

        context.TraceIdentifier = requestId;
        context.Items["RequestId"] = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
        {
            await _next(context);
        }
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
        => app.UseMiddleware<RequestIdMiddleware>();
}
