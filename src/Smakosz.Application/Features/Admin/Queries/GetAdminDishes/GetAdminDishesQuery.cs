using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Constants;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetAdminDishes;

public record GetAdminDishesQuery(
    PaginationParams Pagination,
    string? Search = null,
    int? RestaurantId = null,
    string? ModerationStatus = null,
    bool? IsAvailable = null)
    : IRequest<ErrorOr<PagedResult<AdminDishDto>>>;

public class GetAdminDishesHandler : IRequestHandler<GetAdminDishesQuery, ErrorOr<PagedResult<AdminDishDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminDishesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminDishDto>>> Handle(GetAdminDishesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.Dishes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(d => d.DishName.ToLower().Contains(search));
        }

        if (request.RestaurantId.HasValue)
        {
            query = query.Where(d => d.RestaurantId == request.RestaurantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ModerationStatus) &&
            Enum.TryParse<ContentModerationStatus>(request.ModerationStatus, true, out var statusEnum))
        {
            query = query.Where(d => d.ModerationStatus == statusEnum);
        }

        if (request.IsAvailable.HasValue)
        {
            query = query.Where(d => d.IsAvailable == request.IsAvailable.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(d => new AdminDishDto
            {
                DishId = d.DishId,
                PublicId = d.PublicId,
                DishName = d.DishName,
                Price = d.Price,
                IsAvailable = d.IsAvailable,
                ImageUrl = d.ImageUrl,
                ImageBlurhash = d.ImageBlurhash,
                ModerationStatus = d.ModerationStatus.ToString(),
                AvgRating = d.AvgRating,
                ReviewCount = d.ReviewCount,
                TrendingScore = d.TrendingScore,
                Slug = d.Slug,
                RestaurantId = d.RestaurantId,
                RestaurantName = d.Restaurant != null ? d.Restaurant.RestaurantName : null,
                Ingredients = d.DishIngredients
                    .Select(di => di.Ingredient != null ? di.Ingredient.IngredientName : string.Empty)
                    .Where(n => n != string.Empty)
                    .ToList(),
                Tags = d.DishTags
                    .Select(dt => new AdminTagDto
                    {
                        TagId = dt.Tag.TagId,
                        TagName = dt.Tag.TagName,
                        Category = dt.Tag.Category,
                        TargetEntity = dt.Tag.TargetEntity.ToString(),
                        DisplayColor = dt.Tag.DisplayColor,
                        UsageCount = 0,
                        CreatedAt = dt.Tag.CreatedAt
                    })
                    .ToList(),
                CategoryTagName = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.DishCategory)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                CategoryColor = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.DishCategory)
                    .Select(dt => dt.Tag.DisplayColor)
                    .FirstOrDefault(),
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminDishDto>
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
