using Smakosz.Domain.Entities.System;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.RateLimiting;

public class RateLimitingTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SystemConfigs.AddRange(
                new SystemConfig { Key = "ratelimit.auth.permit_limit", Value = "3" },
                new SystemConfig { Key = "ratelimit.auth.window_seconds", Value = "60" },
                new SystemConfig { Key = "ratelimit.general.permit_limit", Value = "5" },
                new SystemConfig { Key = "ratelimit.general.window_seconds", Value = "60" });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task AuthEndpoint_ExceedsLimit_Returns429()
    {
        for (var i = 0; i < 3; i++)
        {
            await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "test@example.com",
                Password = "pass",
                TurnstileToken = "token"
            });
        }

        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "pass",
            TurnstileToken = "token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GeneralEndpoint_ExceedsLimit_Returns429()
    {
        for (var i = 0; i < 5; i++)
        {
            await AnonymousClient.GetAsync("/api/home");
        }

        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RateLimitResponse_ContainsExpectedBody()
    {
        for (var i = 0; i < 3; i++)
        {
            await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "test@example.com",
                Password = "pass",
                TurnstileToken = "token"
            });
        }

        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "pass",
            TurnstileToken = "token"
        });

        var error = await DeserializeError(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("RATE_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task RateLimitResponse_ContainsRetryAfterHeader()
    {
        for (var i = 0; i < 3; i++)
        {
            await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "test@example.com",
                Password = "pass",
                TurnstileToken = "token"
            });
        }

        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "pass",
            TurnstileToken = "token"
        });

        response.Headers.Contains("Retry-After").Should().BeTrue();
    }
}
