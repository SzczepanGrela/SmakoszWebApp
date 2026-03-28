using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Smakosz.Infrastructure.Logging;

public static class DbLoggerExtensions
{
    public static ILoggingBuilder AddDatabaseLogger(
        this ILoggingBuilder builder,
        Action<DbLoggerOptions>? configure = null)
    {
        var options = new DbLoggerOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ILoggerProvider, DbLoggerProvider>();

        return builder;
    }
}
