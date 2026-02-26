using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class NotificationCleanupService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationCleanupService> _logger;

    public NotificationCleanupService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<NotificationCleanupService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task PruneAsync(CancellationToken ct)
    {
        var cutoff = _clock.UtcNow.AddDays(-90);

        var deleted = await _db.Notifications
            .Where(n => n.CreatedAt < cutoff && n.IsRead)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("prune-notifications: deleted {Count} old read notifications", deleted);
    }
}
