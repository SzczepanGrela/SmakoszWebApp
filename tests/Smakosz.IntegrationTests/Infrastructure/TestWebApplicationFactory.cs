using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Infrastructure.Services;
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
                ["Vapid:PublicKey"] = "",
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
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

            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<SmakoszDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            services.AddScoped<ISmakoszDbContext>(sp =>
                sp.GetRequiredService<SmakoszDbContext>());

            services.RemoveAll<INcfTrainingService>();
            services.AddScoped<INcfTrainingService, StubNcfTrainingService>();

            services.RemoveAll<IRecommendationProvider>();
            services.AddSingleton<IRecommendationProvider, StubRecommendationProvider>();

            services.RemoveAll<IModerationAggregationService>();
            services.AddScoped<IModerationAggregationService, StubModerationAggregationService>();

            services.RemoveAll<ITurnstileService>();
            services.AddScoped<ITurnstileService, StubTurnstileService>();

            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, StubEmailService>();

            services.RemoveAll<IFileStorageService>();
            services.AddScoped<IFileStorageService, StubFileStorageService>();

            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton<IPushNotificationService, StubPushNotificationService>();

            services.RemoveAll<IImageProcessingService>();
            services.AddSingleton<IImageProcessingService, StubImageProcessingService>();

            // Remove Hangfire services - no PostgreSQL in tests
            services.RemoveAll<IBackgroundJobClient>();
            services.RemoveAll<IRecurringJobManager>();

            // Remove database logger - no real DB in tests
            var dbLoggerDescriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(DbLoggerProvider) ||
                (d.ServiceType == typeof(ILoggerProvider) &&
                 d.ImplementationType?.Name == "DbLoggerProvider"));
            if (dbLoggerDescriptor != null)
                services.Remove(dbLoggerDescriptor);
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
