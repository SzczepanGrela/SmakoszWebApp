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

    private static readonly Dictionary<string, TimeSpan> PerTypeStuckThresholds = new()
    {
        ["ncf_training"] = TimeSpan.FromMinutes(30),
        ["text_moderation"] = TimeSpan.FromMinutes(10),
        ["image_moderation"] = TimeSpan.FromMinutes(10),
        ["text_moderation_batch"] = TimeSpan.FromHours(1),
        ["image_moderation_batch"] = TimeSpan.FromHours(1),
    };

    private static readonly TimeSpan DefaultStuckThreshold = TimeSpan.FromHours(4);

    public async Task RecoverAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var widestThreshold = now.Subtract(DefaultStuckThreshold);

        var candidates = await _db.SystemJobs
            .Where(j => j.Status == JobStatus.Processing && j.StartedAt != null)
            .ToListAsync(ct);

        var stuckJobs = candidates
            .Where(j =>
            {
                var threshold = PerTypeStuckThresholds.TryGetValue(j.Type, out var perType) ? perType : DefaultStuckThreshold;
                return j.StartedAt < now.Subtract(threshold);
            })
            .ToList();

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

        if (stuckJobs.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("stuck-jobs-recovery: recovered {Count} stuck jobs", stuckJobs.Count);
        }

        var pendingThreshold = now.AddHours(-24);

        var zombiePending = await _db.SystemJobs
            .Where(j => j.Status == JobStatus.Pending && j.CreatedAt < pendingThreshold)
            .ToListAsync(ct);

        foreach (var job in zombiePending)
        {
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Auto-cancelled: pending for over 24 hours without being picked up";
            job.FinishedAt = now;
        }

        if (zombiePending.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("stuck-jobs-recovery: auto-cancelled {Count} zombie pending jobs", zombiePending.Count);
        }
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
                    case "menu_section":
                        var section = await _db.MenuSections.FirstOrDefaultAsync(ms => ms.SectionId == entityId, ct);
                        if (section is not null) section.ModerationStatus = ContentModerationStatus.Pending;
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
