using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
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
        var cutoff = _clock.UtcNow.AddDays(-30);

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
            await _db.RefreshTokens
                .Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
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
            await _db.MediaAssets
                .Where(ma => ma.UploadedBy == userId).ExecuteDeleteAsync(ct);

            await _db.Users.IgnoreQueryFilters()
                .Where(u => u.UserId == userId).ExecuteDeleteAsync(ct);
        }

        _logger.LogInformation("user-reaper: deleted {Count} users", usersToDelete.Count);
    }
}
