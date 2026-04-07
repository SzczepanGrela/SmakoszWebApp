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
        services.AddInfrastructureCore(connectionString);
        services.AddInfrastructureAuth(configuration);
        services.AddInfrastructureStorage(configuration);
        services.AddInfrastructureRecommendations(configuration);
        services.AddInfrastructureMessaging(configuration);
        services.AddInfrastructureModels(configuration);

        return services;
    }

    public static IServiceCollection AddInfrastructureCore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<SmakoszDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<ISmakoszDbContext>(sp => sp.GetRequiredService<SmakoszDbContext>());
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }

    public static IServiceCollection AddInfrastructureAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CodeHasherOptions>(configuration.GetSection(CodeHasherOptions.SectionName));
        services.AddSingleton<ICodeHasher>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<CodeHasherOptions>>().Value;
            return new HmacCodeHasher(opts.Secret);
        });
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IForbiddenWordService, ForbiddenWordService>();
        services.AddHttpClient<ITurnstileService, TurnstileService>();

        return services;
    }

    public static IServiceCollection AddInfrastructureStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var r2Section = configuration.GetSection(R2Options.SectionName);
        var r2AccountId = r2Section["AccountId"];
        if (!string.IsNullOrEmpty(r2AccountId))
        {
            var r2Options = new R2Options
            {
                AccountId = r2AccountId,
                AccessKey = r2Section["AccessKey"] ?? string.Empty,
                SecretKey = r2Section["SecretKey"] ?? string.Empty,
                BucketName = r2Section["BucketName"] ?? string.Empty,
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

        return services;
    }

    public static IServiceCollection AddInfrastructureRecommendations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        return services;
    }

    public static IServiceCollection AddInfrastructureMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        var vapidSection = configuration.GetSection(VapidOptions.SectionName);
        var vapidPublicKey = vapidSection["PublicKey"];
        if (!string.IsNullOrEmpty(vapidPublicKey))
        {
            var vapidOptions = new VapidOptions
            {
                PublicKey = vapidPublicKey,
                PrivateKey = vapidSection["PrivateKey"] ?? string.Empty,
                Subject = vapidSection["Subject"] ?? string.Empty
            };
            services.AddSingleton(vapidOptions);
            services.AddSingleton<IPushNotificationService, WebPushNotificationService>();
        }
        else
        {
            services.AddSingleton<IPushNotificationService, StubPushNotificationService>();
        }

        return services;
    }

    public static IServiceCollection AddInfrastructureModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
