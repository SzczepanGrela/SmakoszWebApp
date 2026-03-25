using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Infrastructure.Services;

namespace Smakosz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        services.AddDbContext<SmakoszDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<ISmakoszDbContext>(sp => sp.GetRequiredService<SmakoszDbContext>());
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService>(sp =>
            new JwtTokenService(configuration));
        // Email - Brevo if configured, otherwise stub
        var brevoApiKey = configuration.GetSection(BrevoOptions.SectionName)["ApiKey"];
        if (!string.IsNullOrEmpty(brevoApiKey))
        {
            var brevoOptions = new BrevoOptions
            {
                ApiKey = brevoApiKey,
                SenderEmail = configuration.GetSection(BrevoOptions.SectionName)["SenderEmail"] ?? string.Empty,
                SenderName = configuration.GetSection(BrevoOptions.SectionName)["SenderName"] ?? string.Empty
            };
            services.AddSingleton(brevoOptions);
            services.AddHttpClient<IEmailService, BrevoEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, StubEmailService>();
        }

        // File storage - R2 if configured, otherwise stub
        var r2Section = configuration.GetSection(R2Options.SectionName);
        var r2AccountId = r2Section["AccountId"];
        if (!string.IsNullOrEmpty(r2AccountId))
        {
            var r2Options = new R2Options
            {
                AccountId = r2AccountId,
                AccessKey = r2Section["AccessKey"] ?? string.Empty,
                SecretKey = r2Section["SecretKey"] ?? string.Empty,
                BucketName = r2Section["BucketName"] ?? "smakosz-photos",
                PublicUrl = r2Section["PublicUrl"] ?? string.Empty
            };
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(r2Options));
            services.AddSingleton<ImageProcessingService>();
            services.AddScoped<IFileStorageService, R2FileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, StubFileStorageService>();
        }

        return services;
    }
}
