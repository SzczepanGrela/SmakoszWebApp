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
    private readonly ICounterUpdater _counter;

    public UserReaperService(
        SmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<UserReaperService> logger,
        ICounterUpdater counter)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
        _counter = counter;
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
            // Counter triggers fire per row on DELETE; with the triggers gone, decrement helpful_count, dish.review_count and follower counts here before bulk deletes wipe the source rows.
            var dishReviewCounts = await _db.Reviews.IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .GroupBy(r => r.DishId)
                .Select(g => new { DishId = g.Key, Delta = g.Count() })
                .ToListAsync(ct);
            var totalReviews = dishReviewCounts.Sum(x => x.Delta);

            var likedReviewIds = await _db.ReviewLikes
                .Where(rl => rl.UserId == userId)
                .Select(rl => rl.ReviewId)
                .ToListAsync(ct);

            var followedTargets = await _db.UserFollows
                .Where(uf => uf.FollowerId == userId)
                .Select(uf => uf.FollowedId)
                .ToListAsync(ct);
            var followerSources = await _db.UserFollows
                .Where(uf => uf.FollowedId == userId)
                .Select(uf => uf.FollowerId)
                .ToListAsync(ct);

            await _db.Reviews.IgnoreQueryFilters()
                .Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.ReviewLikes
                .Where(rl => rl.UserId == userId).ExecuteDeleteAsync(ct);

            foreach (var entry in dishReviewCounts)
                await _counter.DecrementDishReviewCountAsync(entry.DishId, entry.Delta, ct);
            if (totalReviews > 0)
                await _counter.DecrementUserReviewCountAsync(userId, totalReviews, ct);
            foreach (var reviewId in likedReviewIds)
                await _counter.DecrementHelpfulAsync(reviewId, ct);
            foreach (var followedId in followedTargets)
                await _counter.DecrementFollowersAsync(followedId, ct);
            foreach (var followerId in followerSources)
                await _counter.DecrementFollowingAsync(followerId, ct);
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
                var r2Key = ExtractR2Key(asset.Url);
                if (r2Key.StartsWith("seed/", StringComparison.OrdinalIgnoreCase))
                    continue;
                _db.FilesToDelete.Add(new FileToDelete
                {
                    R2Key = r2Key,
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
                .Where(ma => ma.UploadedBy == userId && EF.Functions.Like(ma.Url, "%seed/%"))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.UploadedBy, _ => null), ct);

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
