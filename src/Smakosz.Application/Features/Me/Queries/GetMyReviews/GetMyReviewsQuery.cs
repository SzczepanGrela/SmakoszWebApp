using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMyReviews;

public record GetMyReviewsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<ReviewCardDto>>>;

public class GetMyReviewsHandler : IRequestHandler<GetMyReviewsQuery, ErrorOr<PagedResult<ReviewCardDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<ReviewCardDto>>> Handle(GetMyReviewsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var likedReviewIds = await _db.ReviewLikes
            .Where(l => l.UserId == userId)
            .Select(l => l.ReviewId)
            .ToListAsync(cancellationToken);

        var result = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewCardDto
            {
                PublicId = r.PublicId,
                DishRating = r.DishRating,
                ServiceRating = r.ServiceRating,
                CleanlinessRating = r.CleanlinessRating,
                AmbianceRating = r.AmbianceRating,
                Content = r.Content,
                ContentStatus = r.ModerationStatus,
                VisitDate = r.VisitDate,
                HelpfulCount = r.HelpfulCount,
                IsHelpfulByMe = likedReviewIds.Contains(r.ReviewId),
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Author = new UserSummaryDto
                {
                    PublicId = r.User.PublicId,
                    Slug = r.User.Slug ?? string.Empty,
                    Username = r.User.Username,
                    AvatarUrl = r.User.AvatarUrl,
                    AvatarBlurhash = r.User.AvatarBlurhash,
                    ReviewCount = r.User.ReviewCount
                },
                DishName = r.Dish.DishName,
                DishSlug = r.Dish.Slug ?? string.Empty,
                RestaurantName = r.Restaurant.RestaurantName,
                RestaurantSlug = r.Restaurant.Slug ?? string.Empty
            })
            .ToPagedResultAsync(request.Pagination, cancellationToken);

        return result;
    }
}
