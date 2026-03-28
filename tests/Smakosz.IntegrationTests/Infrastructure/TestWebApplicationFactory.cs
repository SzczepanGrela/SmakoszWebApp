using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Persistence;
using Smakosz.IntegrationTests.Infrastructure.Stubs;

namespace Smakosz.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"SmakoszTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost",
                ["Jwt:Secret"] = TestAuthHelper.JwtSecret,
                ["Jwt:Issuer"] = TestAuthHelper.JwtIssuer,
                ["Jwt:Audience"] = TestAuthHelper.JwtAudience,
                ["Brevo:ApiKey"] = "",
                ["R2:AccountId"] = "",
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // This includes DbContextOptions<SmakoszDbContext>, the DbContext itself,
            // and any IDbContextOptionsConfiguration<SmakoszDbContext>.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<SmakoszDbContext>) ||
                d.ServiceType == typeof(SmakoszDbContext) ||
                d.ServiceType == typeof(ISmakoszDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IDbContextOptionsConfiguration") == true) ||
                d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true
            ).ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Also remove the generic DbContextOptions (non-generic fallback)
            services.RemoveAll<DbContextOptions>();

            // Re-add with InMemory provider only
            services.AddDbContext<SmakoszDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            services.AddScoped<ISmakoszDbContext>(sp =>
                sp.GetRequiredService<SmakoszDbContext>());

            // Replace NCF training service with stub
            services.RemoveAll<INcfTrainingService>();
            services.AddScoped<INcfTrainingService, StubNcfTrainingService>();

            // Replace recommendation provider with stub
            services.RemoveAll<IRecommendationProvider>();
            services.AddSingleton<IRecommendationProvider, StubRecommendationProvider>();

            // Replace moderation aggregation service with stub
            services.RemoveAll<IModerationAggregationService>();
            services.AddScoped<IModerationAggregationService, StubModerationAggregationService>();
        });
    }

    public HttpClient CreateAnonymousClient()
    {
        return CreateClient();
    }

    public HttpClient CreateUserClient(int userId = 1, string username = "jan-kowalski")
    {
        var token = TestAuthHelper.GenerateJwtToken(userId, username, $"{username}@smakosz.test", "User");
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateAdminClient(int userId = 99, string username = "administrator")
    {
        var token = TestAuthHelper.GenerateJwtToken(userId, username, $"{username}@smakosz.test", "Admin");
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateBusinessClient(int userId = 50, string username = "restaurator")
    {
        var token = TestAuthHelper.GenerateJwtToken(userId, username, $"{username}@smakosz.test", "Restaurant");
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task SeedDataAsync(Func<SmakoszDbContext, Task> seedAction)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();
        await seedAction(db);
    }

    public T GetService<T>() where T : notnull
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
