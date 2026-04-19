using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Persistence;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Middleware;

public class ForwardedHeadersTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Login_WithXForwardedForHeader_SecurityLogCapturesForwardedIp()
    {
        AnonymousClient.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42");

        await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = "WrongPassword123!",
            TurnstileToken = "test-token"
        });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();
        var latest = await db.SecurityLogs.OrderByDescending(l => l.LogId).FirstOrDefaultAsync();

        latest.Should().NotBeNull();
        latest!.IpAddress.Should().Be("203.0.113.42");
    }

    [Fact]
    public async Task Login_WithoutXForwardedForHeader_SecurityLogDoesNotUseForwardedIp()
    {
        await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = "WrongPassword123!",
            TurnstileToken = "test-token"
        });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();
        var latest = await db.SecurityLogs.OrderByDescending(l => l.LogId).FirstOrDefaultAsync();

        latest.Should().NotBeNull();
        latest!.IpAddress.Should().NotBe("203.0.113.42");
    }
}
