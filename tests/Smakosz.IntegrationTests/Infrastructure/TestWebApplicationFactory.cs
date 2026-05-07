using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Services;
using Smakosz.IntegrationTests.Infrastructure.Stubs;

namespace Smakosz.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:PrivateKey"] = TestAuthHelper.JwtPrivateKey,
                ["Jwt:PublicKey"] = TestAuthHelper.JwtPublicKey,
                ["Jwt:Issuer"] = TestAuthHelper.JwtIssuer,
                ["Jwt:Audience"] = TestAuthHelper.JwtAudience,
                ["CodeHasher:Secret"] = "integration-test-code-hasher-secret-min-32-chars",
                ["Brevo:ApiKey"] = "",
                ["R2:AccountId"] = "",
                ["Vapid:PublicKey"] = "",
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
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

            // Hangfire client/server require Postgres-backed storage in prod; tests do not exercise background jobs synchronously.
            services.RemoveAll<IBackgroundJobClient>();
            services.RemoveAll<IRecurringJobManager>();

            // ISendSecurityEmailJob is registered as a Hangfire proxy in API/Program.cs but Hangfire is removed above,
            // so substitute a no-op stub that lets SecurityNotificationService dispatch without enqueueing.
            services.RemoveAll<ISendSecurityEmailJob>();
            services.AddScoped<ISendSecurityEmailJob, StubSendSecurityEmailJob>();

            // DbLoggerProvider writes log events to the real Logs table; tests do not need that side effect.
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

    public async Task SeedDataAsync(Func<Smakosz.Infrastructure.Persistence.SmakoszDbContext, Task> seedAction)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Smakosz.Infrastructure.Persistence.SmakoszDbContext>();
        await seedAction(db);
    }

    public T GetService<T>() where T : notnull
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
