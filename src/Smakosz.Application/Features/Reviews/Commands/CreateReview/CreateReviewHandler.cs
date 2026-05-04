using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Reviews.Commands.CreateReview;

public class CreateReviewHandler : IRequestHandler<CreateReviewCommand, ErrorOr<ReviewCardDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly IBusinessMetrics _metrics;
    private readonly ICounterUpdater _counter;

    public CreateReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords, IBusinessMetrics metrics, ICounterUpdater counter)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
        _metrics = metrics;
        _counter = counter;
    }

    public async Task<ErrorOr<ReviewCardDto>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (_currentUser.Role is not "User" and not "user")
            return DomainErrors.Social.UserRoleOnly;

        var dish = await _db.Dishes
            .Include(d => d.Restaurant)
            .FirstOrDefaultAsync(d => d.PublicId == request.DishPublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var alreadyReviewed = await _db.Reviews.AnyAsync(
            r => r.UserId == _currentUser.UserId.Value && r.DishId == dish.DishId && !r.IsDeleted,
            cancellationToken);

        if (alreadyReviewed)
            return DomainErrors.Review.AlreadyExists;

        if (!string.IsNullOrEmpty(request.Content))
        {
            if (await _forbiddenWords.ContainsAsync(request.Content, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
                return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        }

        var review = new Review
        {
            UserId = _currentUser.UserId.Value,
            DishId = dish.DishId,
            RestaurantId = dish.RestaurantId ?? 0,
            DishRating = request.DishRating,
            ServiceRating = request.ServiceRating,
            CleanlinessRating = request.CleanlinessRating,
            AmbianceRating = request.AmbianceRating,
            Content = request.Content,
            VisitDate = request.VisitDate,
            ModerationStatus = string.IsNullOrEmpty(request.Content) ? ContentModerationStatus.None : ContentModerationStatus.Pending,
            IsVisible = true,
            IsApproved = null
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);
        _metrics.RecordReviewSubmitted();

        await _counter.IncrementUserReviewCountAsync(_currentUser.UserId.Value, cancellationToken);
        await _counter.IncrementDishReviewCountAsync(dish.DishId, cancellationToken);

        if (review.ModerationStatus == ContentModerationStatus.Pending)
        {
            _db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.ReviewContent,
                ReferenceId = review.ReviewId,
                Status = TicketStatus.Open,
                Priority = 3,
                Description = $"Nowa recenzja wymaga moderacji treści"
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        if (dish.Restaurant?.OwnerId is { } ownerId && ownerId != _currentUser.UserId.Value)
        {
            var groupKey = $"review:restaurant:{dish.RestaurantId}";
            var existingNotification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.UserId == ownerId
                    && n.GroupKey == groupKey
                    && !n.IsRead, cancellationToken);

            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == ownerId, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            if (existingNotification != null)
            {
                existingNotification.Counter++;
                existingNotification.ActorId = _currentUser.UserId.Value;
                existingNotification.CreatedAt = DateTime.UtcNow;
                existingNotification.Message = $"Ktoś i {existingNotification.Counter - 1} innych dodało recenzje Twojej restauracji.";
                existingNotification.SendPush = sendPush;
                existingNotification.PushStatus = pushStatus;
            }
            else
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = ownerId,
                    ActorId = _currentUser.UserId.Value,
                    Type = NotificationType.System,
                    Title = "Nowa recenzja",
                    Message = $"Ktoś dodał recenzję dania \"{dish.DishName}\".",
                    GroupKey = groupKey,
                    Counter = 1,
                    SendPush = sendPush,
                    PushStatus = pushStatus,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        var savedReview = await _db.Reviews
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .FirstAsync(r => r.ReviewId == review.ReviewId, cancellationToken);

        return savedReview.ToCardDto(false);
    }
}
