using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessDishes;

public record GetBusinessDishesQuery(int? SectionId = null, int Page = 1, int PageSize = 20) : IRequest<ErrorOr<PagedResult<BusinessDishDto>>>;

public class GetBusinessDishesHandler : IRequestHandler<GetBusinessDishesQuery, ErrorOr<PagedResult<BusinessDishDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidationConfigProvider _config;

    public GetBusinessDishesHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IValidationConfigProvider config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ErrorOr<PagedResult<BusinessDishDto>>> Handle(GetBusinessDishesQuery request, CancellationToken cancellationToken)
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

        var totalCount = await query.CountAsync(cancellationToken);

        var defaultPageSize = _config.GetInt("business.default_page_size", 20);
        var maxPageSize = _config.GetInt("business.max_page_size", 100);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : defaultPageSize, 1, maxPageSize);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var dishes = await query
            .OrderBy(d => d.DishName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new BusinessDishDto
            {
                DishId = d.DishId,
                PublicId = d.PublicId,
                DishName = d.DishName,
                Slug = d.Slug ?? string.Empty,
                Price = d.Price,
                Description = d.Description,
                ImageUrl = d.ImageUrl,
                AvgRating = d.AvgRating,
                ReviewCount = d.ReviewCount,
                IsAvailable = d.IsAvailable
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BusinessDishDto>
        {
            Data = dishes,
            Pagination = new PaginationInfo
            {
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            }
        };
    }
}
