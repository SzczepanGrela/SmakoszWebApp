using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Smakosz.API.Middleware;

namespace Smakosz.UnitTests.Middleware;

[Trait("Category", "Middleware")]
public class HealthCheckAuthMiddlewareTests
{
    private static IConfiguration BuildConfig(string key)
    {
        var config = Substitute.For<IConfiguration>();
        config["Monitoring:HealthCheckKey"].Returns(key);
        return config;
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenPathIsHealthLive()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new HealthCheckAuthMiddleware(next, BuildConfig("secret"));

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/live";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenReadyPathMissingHeader()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new HealthCheckAuthMiddleware(next, BuildConfig("secret"));

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenReadyPathWrongHeader()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new HealthCheckAuthMiddleware(next, BuildConfig("secret"));

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";
        context.Request.Headers["X-Health-Key"] = "wrong";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenReadyPathValidHeader()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new HealthCheckAuthMiddleware(next, BuildConfig("secret"));

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";
        context.Request.Headers["X-Health-Key"] = "secret";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
