using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessDishes;

public record GetBusinessDishesQuery(int? SectionId = null) : IRequest<ErrorOr<List<BusinessDishDto>>>;

public class GetBusinessDishesHandler : IRequestHandler<GetBusinessDishesQuery, ErrorOr<List<BusinessDishDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessDishesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<BusinessDishDto>>> Handle(GetBusinessDishesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var query = _db.Dishes
            .AsNoTracking()
            .Where(d => d.RestaurantId == restaurant.RestaurantId);

        if (request.SectionId.HasValue)
        {
            var dishIds = _db.DishSectionAssignments
                .Where(dsa => dsa.SectionId == request.SectionId.Value)
                .Select(dsa => dsa.DishId);
            query = query.Where(d => dishIds.Contains(d.DishId));
        }

        var dishes = await query
            .OrderBy(d => d.DishName)
            .Select(d => new BusinessDishDto
            {
                DishId = d.DishId,
                DishName = d.DishName,
                Slug = d.Slug ?? string.Empty,
                Price = d.Price,
                Description = d.Description,
                IsAvailable = d.IsAvailable
            })
            .ToListAsync(cancellationToken);

        return dishes;
    }
}
