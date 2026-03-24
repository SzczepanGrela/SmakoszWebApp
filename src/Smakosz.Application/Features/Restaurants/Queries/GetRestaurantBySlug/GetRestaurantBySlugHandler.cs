using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Restaurants.Dtos;

namespace Smakosz.Application.Features.Restaurants.Queries.GetRestaurantBySlug;

public class GetRestaurantBySlugHandler : IRequestHandler<GetRestaurantBySlugQuery, ErrorOr<RestaurantDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRestaurantBySlugHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<RestaurantDetailDto>> Handle(
        GetRestaurantBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Include(r => r.OpeningHours)
            .Include(r => r.MenuSections)
            .FirstOrDefaultAsync(r => r.Slug == request.Slug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var isFavorite = _currentUser.UserId.HasValue &&
            await _db.FavoriteRestaurants.AnyAsync(
                f => f.UserId == _currentUser.UserId.Value && f.RestaurantId == restaurant.RestaurantId,
                cancellationToken);

        return restaurant.ToDetailDto(isFavorite);
    }
}
