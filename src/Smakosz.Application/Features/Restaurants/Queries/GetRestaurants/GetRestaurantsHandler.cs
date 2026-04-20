using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Restaurants.Queries.GetRestaurants;

public class GetRestaurantsHandler : IRequestHandler<GetRestaurantsQuery, ErrorOr<PagedResult<RestaurantCardDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRestaurantsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<RestaurantCardDto>>> Handle(
        GetRestaurantsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Include(r => r.Cuisine)
            .Where(r => r.Status == RestaurantStatus.Active)
            .AsQueryable();

        if (request.CityId.HasValue)
            query = query.Where(r => r.CityId == request.CityId.Value);

        if (request.CuisineTypeId.HasValue)
            query = query.Where(r => r.CuisineTypeId == request.CuisineTypeId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(r => r.PriceLevel >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(r => r.PriceLevel <= request.MaxPrice.Value);

        query = request.SortBy.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(r => r.RestaurantName),
            "rating" => query.OrderByDescending(r => r.AvgFoodScore),
            "price_asc" => query.OrderBy(r => r.PriceLevel),
            "price_desc" => query.OrderByDescending(r => r.PriceLevel),
            _ => query.OrderByDescending(r => r.TrendingScore)
        };

        var favoriteIds = _currentUser.UserId.HasValue
            ? await _db.FavoriteRestaurants
                .Where(f => f.UserId == _currentUser.UserId.Value)
                .Select(f => f.RestaurantId)
                .ToListAsync(cancellationToken)
            : [];

        var result = await query
            .Select(r => new RestaurantCardDto
            {
                PublicId = r.PublicId,
                Slug = r.Slug ?? string.Empty,
                RestaurantName = r.RestaurantName,
                CuisineType = r.Cuisine != null ? r.Cuisine.DisplayName : null,
                CityName = r.City != null ? r.City.CityName : null,
                PriceLevel = r.PriceLevel,
                AvgFoodScore = r.AvgFoodScore,
                ReviewCount = 0,
                ImageUrl = r.ImageUrl,
                ImageBlurhash = r.ImageBlurhash,
                IsFavorite = favoriteIds.Contains(r.RestaurantId)
            })
            .ToPagedResultAsync(request.Pagination, cancellationToken);

        return result;
    }
}
