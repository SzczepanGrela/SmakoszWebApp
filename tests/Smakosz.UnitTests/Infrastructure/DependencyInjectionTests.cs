using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure;

namespace Smakosz.UnitTests.Infrastructure;

[Trait("Category", "DependencyInjection")]
public class DependencyInjectionTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?>? extra = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost",
            ["Jwt:PrivateKey"] = "unused-in-registration",
            ["Jwt:PublicKey"] = "unused-in-registration",
            ["Jwt:Issuer"] = "Test",
            ["Jwt:Audience"] = "Test",
            ["CodeHasher:Secret"] = "test-code-hasher-secret-min-32-chars!!",
            ["Brevo:ApiKey"] = "",
            ["R2:AccountId"] = "",
            ["Vapid:PublicKey"] = "",
        };

        if (extra != null)
            foreach (var kv in extra)
                data[kv.Key] = kv.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void AddInfrastructureCore_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructureCore("Host=localhost");

        var descriptors = services.Select(d => d.ServiceType).ToList();

        descriptors.Should().Contain(typeof(ISmakoszDbContext));
        descriptors.Should().Contain(typeof(IDateTimeProvider));
    }

    [Fact]
    public void AddInfrastructureAuth_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        services.AddInfrastructureAuth(config);

        var descriptors = services.Select(d => d.ServiceType).ToList();

        descriptors.Should().Contain(typeof(IJwtTokenService));
        descriptors.Should().Contain(typeof(ICodeHasher));
        descriptors.Should().Contain(typeof(IPasswordHasher));
        descriptors.Should().Contain(typeof(IForbiddenWordService));
        descriptors.Should().Contain(typeof(ITurnstileService));
    }

    [Fact]
    public void AddInfrastructureStorage_WithoutR2Config_RegistersStubFileStorage()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        services.AddInfrastructureStorage(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFileStorageService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructureRecommendations_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        services.AddInfrastructureRecommendations(config);

        var descriptors = services.Select(d => d.ServiceType).ToList();

        descriptors.Should().Contain(typeof(IRecommendationProvider));
    }

    [Fact]
    public void AddInfrastructureMessaging_WithoutConfig_RegistersStubs()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        services.AddInfrastructureMessaging(config);

        var descriptors = services.Select(d => d.ServiceType).ToList();

        descriptors.Should().Contain(typeof(IEmailService));
        descriptors.Should().Contain(typeof(IPushNotificationService));
    }

    [Fact]
    public void AddInfrastructureModels_WithoutConfig_RegistersNothing()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        var countBefore = services.Count;
        services.AddInfrastructureModels(config);

        services.Count.Should().Be(countBefore);
    }

    [Fact]
    public void AddInfrastructure_RegistersAllServices()
    {
        var services = new ServiceCollection();
        var config = BuildConfig();

        services.AddInfrastructure("Host=localhost", config);

        var descriptors = services.Select(d => d.ServiceType).ToList();

        descriptors.Should().Contain(typeof(ISmakoszDbContext));
        descriptors.Should().Contain(typeof(IDateTimeProvider));
        descriptors.Should().Contain(typeof(IJwtTokenService));
        descriptors.Should().Contain(typeof(ICodeHasher));
        descriptors.Should().Contain(typeof(IPasswordHasher));
        descriptors.Should().Contain(typeof(IForbiddenWordService));
        descriptors.Should().Contain(typeof(ITurnstileService));
        descriptors.Should().Contain(typeof(IFileStorageService));
        descriptors.Should().Contain(typeof(IRecommendationProvider));
        descriptors.Should().Contain(typeof(IEmailService));
        descriptors.Should().Contain(typeof(IPushNotificationService));
    }
}
