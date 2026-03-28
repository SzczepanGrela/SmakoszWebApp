using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Reviews.Queries.GetReviewsByDish;

public class GetReviewsByDishHandler
    : IRequestHandler<GetReviewsByDishQuery, ErrorOr<PagedResult<ReviewCardDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetReviewsByDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<ReviewCardDto>>> Handle(
        GetReviewsByDishQuery request,
        CancellationToken cancellationToken)
    {
        var dish = await _db.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Slug == request.DishSlug, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var likedReviewIds = _currentUser.UserId.HasValue
            ? await _db.ReviewLikes
                .Where(l => l.UserId == _currentUser.UserId.Value)
                .Select(l => l.ReviewId)
                .ToListAsync(cancellationToken)
            : [];

        var query = _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .Where(r => r.DishId == dish.DishId && !r.IsDeleted && r.IsVisible);

        query = request.SortBy.ToLowerInvariant() switch
        {
            "helpful" => query.OrderByDescending(r => r.HelpfulCount),
            "rating_desc" => query.OrderByDescending(r => r.DishRating),
            "rating_asc" => query.OrderBy(r => r.DishRating),
            "oldest" => query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var result = await query
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
