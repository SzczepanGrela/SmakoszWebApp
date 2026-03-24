using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetSavedDishes;

public record GetSavedDishesQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<SavedDishDto>>>;

public class GetSavedDishesHandler : IRequestHandler<GetSavedDishesQuery, ErrorOr<PagedResult<SavedDishDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSavedDishesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<SavedDishDto>>> Handle(GetSavedDishesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var query = _db.SavedDishes
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(s => new SavedDishDto
            {
                DishId = s.DishId,
                DishName = s.Dish.DishName,
                Slug = s.Dish.Slug,
                ImageUrl = s.Dish.ImageUrl,
                RestaurantName = s.Dish.Restaurant != null ? s.Dish.Restaurant.RestaurantName : null,
                RestaurantSlug = s.Dish.Restaurant != null ? s.Dish.Restaurant.Slug : null,
                Price = s.Dish.Price,
                SavedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SavedDishDto>
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
