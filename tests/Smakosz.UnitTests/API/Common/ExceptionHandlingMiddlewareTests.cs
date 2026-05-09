using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using Smakosz.API.Common;

namespace Smakosz.UnitTests.API.Common;

[Trait("Category", "Middleware")]
public class ExceptionHandlingMiddlewareTests
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        _env = Substitute.For<IHostEnvironment>();
        _env.EnvironmentName.Returns("Production");
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns422WithErrors()
    {
        var failures = new[] { new ValidationFailure("Email", "Email is required") };
        RequestDelegate next = _ => throw new ValidationException(failures);
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
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
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
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
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
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

    [Fact]
    public async Task InvokeAsync_PostgresExceptionTooManyConnections_Returns503WithRetryAfter()
    {
        var pgEx = new PostgresException("FATAL: too many connections", "FATAL", "FATAL", "53300");
        RequestDelegate next = _ => throw pgEx;
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);
        context.Response.Headers.RetryAfter.ToString().Should().Be("5");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("DATABASE_UNAVAILABLE");
    }

    [Fact]
    public async Task InvokeAsync_NpgsqlExceptionTransient_Returns503WithRetryAfter()
    {
        RequestDelegate next = _ => throw new FakeTransientNpgsqlException();
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);
        context.Response.Headers.RetryAfter.ToString().Should().Be("5");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("error").GetProperty("code").GetString().Should().Be("DATABASE_UNAVAILABLE");
    }

    [Fact]
    public async Task InvokeAsync_DbUpdateExceptionWrappingPostgresException_Returns503ViaUnwrap()
    {
        var pgEx = new PostgresException("FATAL: too many connections", "FATAL", "FATAL", "53300");
        var dbEx = new DbUpdateException("Update failed", pgEx);
        RequestDelegate next = _ => throw dbEx;
        var middleware = new ExceptionHandlingMiddleware(next, _logger, _env);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        var root = json.RootElement;

        root.GetProperty("error").GetProperty("code").GetString().Should().Be("DATABASE_UNAVAILABLE");
    }

    private sealed class FakeTransientNpgsqlException : NpgsqlException
    {
        public FakeTransientNpgsqlException() : base("Connection pool exhausted") { }
        public override bool IsTransient => true;
    }
}
