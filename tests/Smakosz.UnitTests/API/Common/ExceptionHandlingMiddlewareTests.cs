using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Smakosz.API.Common;

namespace Smakosz.UnitTests.API.Common;

[Trait("Category", "Middleware")]
public class ExceptionHandlingMiddlewareTests
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ExceptionHandlingMiddleware(next, _logger);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns422WithErrors()
    {
        var failures = new[] { new ValidationFailure("Email", "Email is required") };
        RequestDelegate next = _ => throw new ValidationException(failures);
        var middleware = new ExceptionHandlingMiddleware(next, _logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(422);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task InvokeAsync_WhenDbUpdateConcurrencyException_Returns409Conflict()
    {
        RequestDelegate next = _ => throw new DbUpdateConcurrencyException("Concurrency conflict");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(409);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Something broke");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
    }
}
