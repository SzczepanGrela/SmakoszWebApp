using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Dtos;

namespace Smakosz.Application.Features.Dishes.Queries.GetDishesByRestaurant;

public class GetDishesByRestaurantHandler
    : IRequestHandler<GetDishesByRestaurantQuery, ErrorOr<PagedResult<DishCardDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDishesByRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<DishCardDto>>> Handle(
        GetDishesByRestaurantQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Slug == request.RestaurantSlug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var savedDishIds = _currentUser.UserId.HasValue
            ? await _db.SavedDishes
                .Where(s => s.UserId == _currentUser.UserId.Value)
                .Select(s => s.DishId)
                .ToListAsync(cancellationToken)
            : [];

        var result = await _db.Dishes
            .Include(d => d.Restaurant)
            .Where(d => d.RestaurantId == restaurant.RestaurantId && d.IsAvailable)
            .OrderBy(d => d.DishName)
            .Select(d => new DishCardDto
            {
                PublicId = d.PublicId,
                Slug = d.Slug ?? string.Empty,
                DishName = d.DishName,
                Price = d.Price,
                AvgRating = d.AvgRating,
                ReviewCount = d.ReviewCount,
                ImageUrl = d.ImageUrl,
                ImageBlurhash = d.ImageBlurhash,
                RestaurantName = d.Restaurant != null ? d.Restaurant.RestaurantName : null,
                RestaurantSlug = d.Restaurant != null ? d.Restaurant.Slug : null,
                IsVegetarian = d.IsVegetarian,
                IsVegan = d.IsVegan,
                IsGlutenFree = d.IsGlutenFree,
                IsSaved = savedDishIds.Contains(d.DishId)
            })
            .ToPagedResultAsync(request.Pagination, cancellationToken);

        return result;
    }
}
