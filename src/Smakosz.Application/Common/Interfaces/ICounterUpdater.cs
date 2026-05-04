namespace Smakosz.Application.Common.Interfaces;

public interface ICounterUpdater
{
    Task IncrementFollowersAsync(int userId, CancellationToken ct);
    Task DecrementFollowersAsync(int userId, CancellationToken ct);
    Task IncrementFollowingAsync(int userId, CancellationToken ct);
    Task DecrementFollowingAsync(int userId, CancellationToken ct);
    Task IncrementHelpfulAsync(int reviewId, CancellationToken ct);
    Task DecrementHelpfulAsync(int reviewId, CancellationToken ct);
    Task IncrementUserReviewCountAsync(int userId, CancellationToken ct);
    Task DecrementUserReviewCountAsync(int userId, int delta, CancellationToken ct);
    Task IncrementDishReviewCountAsync(int dishId, CancellationToken ct);
    Task DecrementDishReviewCountAsync(int dishId, int delta, CancellationToken ct);
}
