using System.Text.Json;
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

    private static readonly HashSet<string> BatchJobTypes = ["text_moderation_batch", "image_moderation_batch"];

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

                if (BatchJobTypes.Contains(job.Type) && !string.IsNullOrEmpty(job.Payload))
                    await ResetBatchModerationStatusAsync(job, ct);
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

    private async Task ResetBatchModerationStatusAsync(Domain.Entities.System.SystemJob job, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(job.Payload!);
            var items = doc.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                var entityType = item.GetProperty("entity_type").GetString();
                var entityId = item.GetProperty("entity_id").GetInt32();

                switch (entityType)
                {
                    case "review":
                        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == entityId, ct);
                        if (review is not null) review.ModerationStatus = ContentModerationStatus.Pending;
                        break;
                    case "edit_request":
                        var er = await _db.RestaurantEditRequests.FirstOrDefaultAsync(e => e.RequestId == entityId, ct);
                        if (er is not null) er.ModerationStatus = ContentModerationStatus.Pending;
                        break;
                    case "dish":
                        var dish = await _db.Dishes.FirstOrDefaultAsync(d => d.DishId == entityId, ct);
                        if (dish is not null) dish.ModerationStatus = ContentModerationStatus.Pending;
                        break;
                    case "restaurant":
                        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.RestaurantId == entityId, ct);
                        if (restaurant is not null) restaurant.ModerationStatus = ContentModerationStatus.Pending;
                        break;
                    case "media_asset":
                        var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.AssetId == entityId, ct);
                        if (asset is not null) asset.ModerationStatus = ContentModerationStatus.Pending;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset ModerationStatus for stuck batch job {JobId}", job.JobId);
        }
    }
}
