using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

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

            var groupKey = $"like:review:{review.ReviewId}";
            var existingNotification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.UserId == review.UserId
                    && n.GroupKey == groupKey
                    && !n.IsRead, cancellationToken);

            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == review.UserId, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.Like);

            if (existingNotification != null)
            {
                existingNotification.Counter++;
                existingNotification.ActorId = _currentUser.UserId.Value;
                existingNotification.CreatedAt = DateTime.UtcNow;
                existingNotification.Message = $"Ktoś i {existingNotification.Counter - 1} innych polubiło Twoją recenzję.";
                existingNotification.SendPush = sendPush;
                existingNotification.PushStatus = pushStatus;
            }
            else
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = review.UserId,
                    ActorId = _currentUser.UserId.Value,
                    Type = NotificationType.Like,
                    Title = "Polubienie recenzji",
                    Message = "Ktoś polubił Twoją recenzję.",
                    GroupKey = groupKey,
                    Counter = 1,
                    SendPush = sendPush,
                    PushStatus = pushStatus,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ToggleReviewLikeResult(
            IsLiked: existingLike is null,
            HelpfulCount: review.HelpfulCount);
    }
}
