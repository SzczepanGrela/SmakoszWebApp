using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.Orchestrator.Jobs;

public class UserReaperService
{
    private readonly SmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UserReaperService> _logger;

    public UserReaperService(
        SmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<UserReaperService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task ReapAsync(CancellationToken ct)
    {
        var graceDays = await GetIntConfigAsync("retention.user_deletion_grace_days", 30, ct);
        var cutoff = _clock.UtcNow.AddDays(-graceDays);

        var usersToDelete = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsDeleted && u.DeletedAt < cutoff)
            .Select(u => u.UserId)
            .ToListAsync(ct);

        if (usersToDelete.Count == 0)
            return;

        foreach (var userId in usersToDelete)
        {
            await _db.Reviews.IgnoreQueryFilters()
                .Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.ReviewLikes
                .Where(rl => rl.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.Notifications
                .Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.UserSessions
                .Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.VerificationCodes
                .Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.SavedDishes
                .Where(sd => sd.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.FavoriteRestaurants
                .Where(fr => fr.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.UserFollows
                .Where(uf => uf.FollowerId == userId || uf.FollowedId == userId).ExecuteDeleteAsync(ct);
            await _db.SearchHistories
                .Where(sh => sh.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.UserNotificationSettings
                .Where(uns => uns.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.PushSubscriptions
                .Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);

            var mediaUrls = await _db.MediaAssets
                .Where(ma => ma.UploadedBy == userId)
                .Select(ma => new { ma.AssetId, ma.Url })
                .ToListAsync(ct);

            foreach (var asset in mediaUrls)
            {
                _db.FilesToDelete.Add(new FileToDelete
                {
                    R2Key = ExtractR2Key(asset.Url),
                    Bucket = "smakosz-photos",
                    Reason = "user_hard_delete",
                    SourceEntity = "MediaAsset",
                    SourceId = (int)asset.AssetId,
                    QueuedAt = _clock.UtcNow
                });
            }
            if (mediaUrls.Count > 0)
                await _db.SaveChangesAsync(ct);

            await _db.MediaAssets
                .Where(ma => ma.UploadedBy == userId).ExecuteDeleteAsync(ct);

            await _db.Users.IgnoreQueryFilters()
                .Where(u => u.UserId == userId).ExecuteDeleteAsync(ct);
        }

        _logger.LogInformation("user-reaper: deleted {Count} users", usersToDelete.Count);
    }

    private static string ExtractR2Key(string url) => new Uri(url).AbsolutePath.TrimStart('/');

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
