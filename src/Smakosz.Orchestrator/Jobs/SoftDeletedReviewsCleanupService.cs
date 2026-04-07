using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.Orchestrator.Jobs;

public class SoftDeletedReviewsCleanupService
{
    private readonly SmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SoftDeletedReviewsCleanupService> _logger;

    public SoftDeletedReviewsCleanupService(
        SmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<SoftDeletedReviewsCleanupService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.reviews_days", 180, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);

        var deleted = await _db.Reviews
            .IgnoreQueryFilters()
            .Where(r => r.IsDeleted && r.DeletedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("soft-deleted-reviews-cleanup: deleted {Count} old reviews", deleted);
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
