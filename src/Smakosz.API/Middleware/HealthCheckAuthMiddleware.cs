using Microsoft.Extensions.Primitives;

namespace Smakosz.API.Middleware;

public class HealthCheckAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public HealthCheckAuthMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health/ready"))
        {
            var expected = _config["Monitoring:HealthCheckKey"];
            context.Request.Headers.TryGetValue("X-Health-Key", out var headerValue);

            if (!string.Equals(headerValue.ToString(), expected, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }

        await _next(context);
    }
}
