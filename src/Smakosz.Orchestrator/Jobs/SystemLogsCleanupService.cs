using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class SystemLogsCleanupService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SystemLogsCleanupService> _logger;

    public SystemLogsCleanupService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<SystemLogsCleanupService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.system_logs_days", 90, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);

        var deleted = await _db.SystemLogs
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("system-logs-cleanup: deleted {Count} old logs", deleted);
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
