using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetFavoriteRestaurants;

public record GetFavoriteRestaurantsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<FavoriteRestaurantDto>>>;

public class GetFavoriteRestaurantsHandler : IRequestHandler<GetFavoriteRestaurantsQuery, ErrorOr<PagedResult<FavoriteRestaurantDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetFavoriteRestaurantsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<FavoriteRestaurantDto>>> Handle(GetFavoriteRestaurantsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var query = _db.FavoriteRestaurants
            .AsNoTracking()
            .Where(f => f.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(f => new FavoriteRestaurantDto
            {
                RestaurantId = f.RestaurantId,
                Name = f.Restaurant.RestaurantName,
                Slug = f.Restaurant.Slug,
                ImageUrl = f.Restaurant.ImageUrl,
                CuisineType = f.Restaurant.Cuisine != null ? f.Restaurant.Cuisine.DisplayName : null,
                AvgRating = f.Restaurant.AvgFoodScore,
                FavoritedAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FavoriteRestaurantDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize)
            }
        };
    }
}
