using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessReviews;

public record GetBusinessReviewsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<ReviewCardDto>>>;

public class GetBusinessReviewsHandler : IRequestHandler<GetBusinessReviewsQuery, ErrorOr<PagedResult<ReviewCardDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidationConfigProvider _config;

    public GetBusinessReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IValidationConfigProvider config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ErrorOr<PagedResult<ReviewCardDto>>> Handle(
        GetBusinessReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var maxPageSize = _config.GetInt("business.max_page_size", 100);

        var result = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .Where(r => r.RestaurantId == restaurant.RestaurantId && !r.IsDeleted)
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
                IsHelpfulByMe = false,
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
            .ToPagedResultAsync(request.Pagination, maxPageSize, cancellationToken);

        return result;
    }
}
