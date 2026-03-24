using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class StuckJobsRecoveryService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<StuckJobsRecoveryService> _logger;

    public StuckJobsRecoveryService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<StuckJobsRecoveryService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task RecoverAsync(CancellationToken ct)
    {
        var threshold = _clock.UtcNow.AddHours(-4);

        var stuckJobs = await _db.SystemJobs
            .Where(j => j.Status == JobStatus.Processing && j.StartedAt < threshold)
            .ToListAsync(ct);

        if (stuckJobs.Count == 0)
            return;

        var now = _clock.UtcNow;

        foreach (var job in stuckJobs)
        {
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = "Exceeded max attempts after being stuck in Processing";
                job.FinishedAt = now;
            }
            else
            {
                job.Status = JobStatus.Pending;
                job.WorkerNode = null;
                job.StartedAt = null;
                job.Progress = 0;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("stuck-jobs-recovery: recovered {Count} stuck jobs", stuckJobs.Count);
    }
}
