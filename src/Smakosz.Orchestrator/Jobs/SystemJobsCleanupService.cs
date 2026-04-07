using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class SystemJobsCleanupService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SystemJobsCleanupService> _logger;

    public SystemJobsCleanupService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<SystemJobsCleanupService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.system_jobs_days", 30, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);

        var deleted = await _db.SystemJobs
            .Where(j => (j.Status == JobStatus.Completed || j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled)
                && j.FinishedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("system-jobs-cleanup: deleted {Count} old jobs", deleted);
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
