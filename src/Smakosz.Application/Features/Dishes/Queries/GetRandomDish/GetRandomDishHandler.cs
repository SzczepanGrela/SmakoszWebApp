using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Dishes.Queries.GetRandomDish;

public class GetRandomDishHandler : IRequestHandler<GetRandomDishQuery, ErrorOr<DishCardDto>>
{
    private readonly ISmakoszDbContext _db;

    public GetRandomDishHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<DishCardDto>> Handle(GetRandomDishQuery request, CancellationToken cancellationToken)
    {
        var dish = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .OrderBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync(cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        return dish.ToCardDto(false);
    }
}
