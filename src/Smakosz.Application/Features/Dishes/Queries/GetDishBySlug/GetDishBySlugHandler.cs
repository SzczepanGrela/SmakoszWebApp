using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Dishes.Dtos;

namespace Smakosz.Application.Features.Dishes.Queries.GetDishBySlug;

public class GetDishBySlugHandler : IRequestHandler<GetDishBySlugQuery, ErrorOr<DishDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDishBySlugHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<DishDetailDto>> Handle(GetDishBySlugQuery request, CancellationToken cancellationToken)
    {
        var dish = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
                .ThenInclude(r => r!.City)
            .FirstOrDefaultAsync(d => d.Slug == request.Slug, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var isSaved = _currentUser.UserId.HasValue &&
            await _db.SavedDishes.AnyAsync(
                s => s.UserId == _currentUser.UserId.Value && s.DishId == dish.DishId,
                cancellationToken);

        return dish.ToDetailDto(isSaved);
    }
}
