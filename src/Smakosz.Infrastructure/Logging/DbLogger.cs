using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence;
using DomainLogLevel = Smakosz.Domain.Enums.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Smakosz.Infrastructure.Logging;

public sealed class DbLogger : ILogger
{
    private readonly string _category;
    private readonly string _shortCategory;
    private readonly DbLoggerProvider _provider;

    public DbLogger(string category, DbLoggerProvider provider)
    {
        _category = category;
        _shortCategory = GetShortCategory(category);
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(MsLogLevel logLevel)
    {
        if (logLevel is MsLogLevel.None or MsLogLevel.Trace or MsLogLevel.Debug)
            return false;

        if (logLevel < _provider.Options.MinLevel)
            return false;

        foreach (var prefix in _provider.Options.IgnoredPrefixes)
        {
            if (_category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public void Log<TState>(MsLogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var domainLevel = MapLevel(logLevel);
        if (domainLevel is null)
            return;

        string? context = null;
        if (exception is not null)
        {
            context = JsonSerializer.Serialize(new
            {
                Type = exception.GetType().FullName,
                exception.StackTrace,
                InnerException = exception.InnerException?.Message
            });
        }

        var entry = new SystemLog
        {
            Source = _shortCategory,
            Level = domainLevel.Value,
            Message = formatter(state, exception),
            Context = context,
            CreatedAt = DateTime.UtcNow
        };

        _provider.Enqueue(entry);
    }

    private static DomainLogLevel? MapLevel(MsLogLevel level) => level switch
    {
        MsLogLevel.Information => DomainLogLevel.Info,
        MsLogLevel.Warning => DomainLogLevel.Warning,
        MsLogLevel.Error => DomainLogLevel.Error,
        MsLogLevel.Critical => DomainLogLevel.Critical,
        _ => null
    };

    private static string GetShortCategory(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 ? category[(lastDot + 1)..] : category;
    }
}
