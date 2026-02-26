using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class ConfigPollingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigPollingService> _logger;
    private readonly ConcurrentDictionary<string, (string Value, DateTime? UpdatedAt)> _cache = new();

    public ConfigPollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConfigPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PollAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();

        var configs = await db.SystemConfigs.ToListAsync(ct);

        var changed = new List<string>();

        foreach (var config in configs)
        {
            var newEntry = (config.Value, config.UpdatedAt);

            if (_cache.TryGetValue(config.Key, out var existing))
            {
                if (existing.Value != config.Value || existing.UpdatedAt != config.UpdatedAt)
                {
                    changed.Add(config.Key);
                    _cache[config.Key] = newEntry;
                }
            }
            else
            {
                _cache[config.Key] = newEntry;
            }
        }

        // Remove keys that no longer exist in DB
        foreach (var key in _cache.Keys)
        {
            if (configs.All(c => c.Key != key))
            {
                _cache.TryRemove(key, out _);
                changed.Add($"{key} (removed)");
            }
        }

        if (changed.Count > 0)
        {
            _logger.LogInformation(
                "config-polling: detected changes in {Count} keys: {Keys}",
                changed.Count, string.Join(", ", changed));
        }
    }
}
