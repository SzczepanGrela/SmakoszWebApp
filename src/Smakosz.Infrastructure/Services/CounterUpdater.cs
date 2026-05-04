using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class CounterUpdater : ICounterUpdater
{
    private readonly ISmakoszDbContext _db;

    public CounterUpdater(ISmakoszDbContext db) => _db = db;

    public Task IncrementFollowersAsync(int userId, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowersCount, u => u.FollowersCount + 1), ct);

    public Task DecrementFollowersAsync(int userId, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId && u.FollowersCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowersCount, u => u.FollowersCount - 1), ct);

    public Task IncrementFollowingAsync(int userId, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount + 1), ct);

    public Task DecrementFollowingAsync(int userId, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId && u.FollowingCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount - 1), ct);

    public Task IncrementHelpfulAsync(int reviewId, CancellationToken ct) =>
        _db.Reviews.Where(r => r.ReviewId == reviewId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HelpfulCount, r => r.HelpfulCount + 1), ct);

    public Task DecrementHelpfulAsync(int reviewId, CancellationToken ct) =>
        _db.Reviews.Where(r => r.ReviewId == reviewId && r.HelpfulCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HelpfulCount, r => r.HelpfulCount - 1), ct);

    public Task IncrementUserReviewCountAsync(int userId, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ReviewCount, u => u.ReviewCount + 1), ct);

    public Task DecrementUserReviewCountAsync(int userId, int delta, CancellationToken ct) =>
        _db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ReviewCount, u => u.ReviewCount - delta < 0 ? 0 : u.ReviewCount - delta), ct);

    public Task IncrementDishReviewCountAsync(int dishId, CancellationToken ct) =>
        _db.Dishes.Where(d => d.DishId == dishId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReviewCount, d => d.ReviewCount + 1), ct);

    public Task DecrementDishReviewCountAsync(int dishId, int delta, CancellationToken ct) =>
        _db.Dishes.Where(d => d.DishId == dishId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReviewCount, d => d.ReviewCount - delta < 0 ? 0 : d.ReviewCount - delta), ct);
}
