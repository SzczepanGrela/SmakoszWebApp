using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

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
        var socialDays = await GetIntConfigAsync("retention.notifications_social_days", 30, ct);
        var systemDays = await GetIntConfigAsync("retention.notifications_system_days", 365, ct);

        var socialCutoff = _clock.UtcNow.AddDays(-socialDays);
        var systemCutoff = _clock.UtcNow.AddDays(-systemDays);

        var deletedSocial = await _db.Notifications
            .Where(n => n.IsRead
                && (n.Type == NotificationType.Like || n.Type == NotificationType.Follow)
                && n.CreatedAt < socialCutoff)
            .ExecuteDeleteAsync(ct);

        var deletedSystem = await _db.Notifications
            .Where(n => n.IsRead
                && (n.Type == NotificationType.System || n.Type == NotificationType.Security)
                && n.CreatedAt < systemCutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "prune-notifications: deleted {Social} social, {System} system read notifications",
            deletedSocial, deletedSystem);
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
