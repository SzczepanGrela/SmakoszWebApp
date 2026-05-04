using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class ReviewVisibilityRecalculator : IReviewVisibilityRecalculator
{
    private readonly ISmakoszDbContext _db;

    public ReviewVisibilityRecalculator(ISmakoszDbContext db) => _db = db;

    public async Task EvaluateAsync(int reviewId, CancellationToken ct)
    {
        var review = await _db.Reviews
            .Where(r => r.ReviewId == reviewId)
            .Select(r => new { r.ModerationStatus, r.IsVisible })
            .FirstOrDefaultAsync(ct);
        if (review is null) return;

        var hasPendingPhotos = await _db.MediaAssets
            .AnyAsync(m => m.EntityType == MediaEntityType.Review
                        && m.EntityId == reviewId
                        && m.ModerationStatus == ContentModerationStatus.Pending, ct);
        var hasRejectedPhotos = await _db.MediaAssets
            .AnyAsync(m => m.EntityType == MediaEntityType.Review
                        && m.EntityId == reviewId
                        && m.ModerationStatus == ContentModerationStatus.Rejected, ct);

        var contentVisible = review.ModerationStatus == ContentModerationStatus.Approved
                          || review.ModerationStatus == ContentModerationStatus.None;
        var newVisibility = contentVisible && !hasPendingPhotos && !hasRejectedPhotos;

        if (review.IsVisible != newVisibility)
        {
            await _db.Reviews
                .Where(r => r.ReviewId == reviewId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsVisible, newVisibility), ct);
        }
    }
}
