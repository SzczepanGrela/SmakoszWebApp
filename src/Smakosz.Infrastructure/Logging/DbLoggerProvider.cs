using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.Infrastructure.Logging;

[ProviderAlias("Database")]
public sealed class DbLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentQueue<SystemLog> _queue = new();
    private readonly Timer _flushTimer;
    private readonly ConcurrentDictionary<string, DbLogger> _loggers = new();
    private bool _disposed;

    public DbLoggerOptions Options { get; }

    public DbLoggerProvider(IServiceScopeFactory scopeFactory, DbLoggerOptions options)
    {
        _scopeFactory = scopeFactory;
        Options = options;
        _flushTimer = new Timer(_ => _ = FlushAsync(), null, options.FlushInterval, options.FlushInterval);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new DbLogger(name, this));
    }

    internal void Enqueue(SystemLog entry)
    {
        if (_disposed) return;

        _queue.Enqueue(entry);

        if (_queue.Count >= Options.BatchSize)
            _ = FlushAsync();
    }

    private async Task FlushAsync()
    {
        if (_queue.IsEmpty) return;

        var batch = new List<SystemLog>();
        while (_queue.TryDequeue(out var entry))
            batch.Add(entry);

        if (batch.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();
            db.SystemLogs.AddRange(batch);
            await db.SaveChangesAsync();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer.Dispose();
        FlushAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _flushTimer.DisposeAsync();
        await FlushAsync();
    }
}
