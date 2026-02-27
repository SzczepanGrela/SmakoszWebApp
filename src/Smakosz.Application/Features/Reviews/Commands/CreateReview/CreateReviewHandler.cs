using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
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

    public CreateReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<ReviewCardDto>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

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
            var contentLower = request.Content.ToLowerInvariant();
            var hasForbiddenWord = await _db.ForbiddenWords
                .Where(fw => fw.Category == ForbiddenWordCategory.Profanity || fw.Category == ForbiddenWordCategory.Offensive)
                .AnyAsync(fw => !fw.IsRegex && contentLower.Contains(fw.Word.ToLower()), cancellationToken);

            if (hasForbiddenWord)
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
            ContentStatus = string.IsNullOrEmpty(request.Content) ? ReviewContentStatus.None : ReviewContentStatus.Pending,
            IsVisible = true,
            IsApproved = false
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        if (review.ContentStatus == ReviewContentStatus.Pending)
        {
            _db.SystemJobs.Add(new SystemJob
            {
                Type = "text_moderation",
                Status = JobStatus.Pending,
                Priority = 5,
                EntityId = review.ReviewId.ToString(),
                EntityType = "review",
                Payload = JsonSerializer.Serialize(new
                {
                    review_id = review.ReviewId,
                    text = review.Content,
                    language = "pl"
                })
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (dish.Restaurant?.OwnerId is { } ownerId && ownerId != _currentUser.UserId.Value)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = ownerId,
                ActorId = _currentUser.UserId.Value,
                Type = NotificationType.System,
                Title = "Nowa recenzja",
                Message = $"Ktoś dodał recenzję dania \"{dish.DishName}\".",
                GroupKey = $"review:restaurant:{dish.RestaurantId}",
                CreatedAt = DateTime.UtcNow
            });
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
