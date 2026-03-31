using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Reviews.Commands.ToggleReviewLike;

public record ToggleReviewLikeCommand(Guid ReviewPublicId) : IRequest<ErrorOr<ToggleReviewLikeResult>>;

public record ToggleReviewLikeResult(bool IsLiked, int HelpfulCount);

public class ToggleReviewLikeHandler : IRequestHandler<ToggleReviewLikeCommand, ErrorOr<ToggleReviewLikeResult>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ToggleReviewLikeHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<ToggleReviewLikeResult>> Handle(ToggleReviewLikeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.ReviewPublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        if (review.UserId == _currentUser.UserId.Value)
            return DomainErrors.ReviewLike.CannotLikeOwnReview;

        var existingLike = await _db.ReviewLikes
            .FirstOrDefaultAsync(l => l.UserId == _currentUser.UserId.Value && l.ReviewId == review.ReviewId, cancellationToken);

        if (existingLike is not null)
        {
            _db.ReviewLikes.Remove(existingLike);
            review.HelpfulCount = Math.Max(0, review.HelpfulCount - 1);
        }
        else
        {
            _db.ReviewLikes.Add(new ReviewLike
            {
                UserId = _currentUser.UserId.Value,
                ReviewId = review.ReviewId,
                CreatedAt = DateTime.UtcNow
            });
            review.HelpfulCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ToggleReviewLikeResult(
            IsLiked: existingLike is null,
            HelpfulCount: review.HelpfulCount);
    }
}
