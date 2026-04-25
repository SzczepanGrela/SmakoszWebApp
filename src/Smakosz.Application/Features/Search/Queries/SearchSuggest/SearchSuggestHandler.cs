using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Search.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Search.Queries.SearchSuggest;

public class SearchSuggestHandler : IRequestHandler<SearchSuggestQuery, ErrorOr<List<SuggestItemDto>>>
{
    private readonly ISmakoszDbContext _db;

    public SearchSuggestHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<List<SuggestItemDto>>> Handle(SearchSuggestQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length < 2)
            return new List<SuggestItemDto>();

        var term = request.Query.Trim().ToLower();
        var limit = Math.Clamp(request.Limit, 1, 10);

        var dishes = await SearchDishes(term, limit, ct);
        var remaining = limit - dishes.Count;
        var restaurants = remaining > 0
            ? await SearchRestaurants(term, remaining, ct)
            : [];

        return dishes.Concat(restaurants).ToList();
    }

    private async Task<List<SuggestItemDto>> SearchDishes(string term, int limit, CancellationToken ct)
    {
        return await _db.Dishes
            .AsNoTracking()
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .Where(d => EF.Functions.ILike(d.DishName, $"%{term}%"))
            .OrderBy(d => !EF.Functions.ILike(d.DishName, $"{term}%"))
            .ThenByDescending(d => d.ReviewCount)
            .Take(limit)
            .Select(d => new SuggestItemDto
            {
                Type = "dish",
                Name = d.DishName,
                Slug = d.Slug ?? string.Empty,
                Subtitle = d.Restaurant != null ? d.Restaurant.RestaurantName : null,
                ImageUrl = d.ImageUrl != null ? d.ImageUrl.Replace(".webp", "_tiny.webp") : null,
                ImageBlurhash = d.ImageBlurhash
            })
            .ToListAsync(ct);
    }

    private async Task<List<SuggestItemDto>> SearchRestaurants(string term, int limit, CancellationToken ct)
    {
        return await _db.Restaurants
            .AsNoTracking()
            .Include(r => r.Cuisine)
            .Where(r => r.Status == RestaurantStatus.Active)
            .Where(r => EF.Functions.ILike(r.RestaurantName, $"%{term}%"))
            .OrderBy(r => !EF.Functions.ILike(r.RestaurantName, $"{term}%"))
            .ThenByDescending(r => r.AvgFoodScore)
            .Take(limit)
            .Select(r => new SuggestItemDto
            {
                Type = "restaurant",
                Name = r.RestaurantName,
                Slug = r.Slug ?? string.Empty,
                Subtitle = r.Cuisine != null ? r.Cuisine.DisplayName : null,
                ImageUrl = r.ImageUrl != null ? r.ImageUrl.Replace(".webp", "_tiny.webp") : null,
                ImageBlurhash = r.ImageBlurhash
            })
            .ToListAsync(ct);
    }
}
