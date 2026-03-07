using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;
using Smakosz.Infrastructure.Logging;
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
        services.AddMemoryCache();
        services.AddScoped<IForbiddenWordService, ForbiddenWordService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ICodeHasher>(sp =>
            new HmacCodeHasher(configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is required for ICodeHasher")));
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
            services.AddSingleton<IImageProcessingService, ImageProcessingService>();
            services.AddScoped<IFileStorageService, R2FileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, StubFileStorageService>();
        }

        // ONNX Recommendation - strategy pattern
        services.Configure<OnnxOptions>(configuration.GetSection(OnnxOptions.SectionName));
        services.AddSingleton<OnnxRecommendationService>();
        services.AddSingleton<TrendingRecommendationService>();
        services.AddSingleton<IRecommendationProvider>(sp =>
        {
            var onnx = sp.GetRequiredService<OnnxRecommendationService>();
            if (onnx.IsAvailable)
                return onnx;
            return sp.GetRequiredService<TrendingRecommendationService>();
        });

        // NCF Model Storage - R2 Models bucket
        var r2ModelsSection = configuration.GetSection(R2ModelOptions.SectionName);
        var r2ModelsAccountId = r2ModelsSection["AccountId"];
        if (!string.IsNullOrEmpty(r2ModelsAccountId))
        {
            services.Configure<R2ModelOptions>(r2ModelsSection);
            services.AddSingleton<INcfModelStorageService, NcfModelStorageService>();
        }

        return services;
    }

    public static ILoggingBuilder AddSmakoszDbLogging(
        this ILoggingBuilder builder,
        Action<DbLoggerOptions>? configure = null)
    {
        return builder.AddDatabaseLogger(configure);
    }
}
