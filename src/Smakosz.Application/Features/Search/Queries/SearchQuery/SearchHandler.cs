using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Search.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Search.Queries.SearchQuery;

public class SearchHandler : IRequestHandler<SearchQuery, ErrorOr<SearchResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<SearchResultDto>> Handle(SearchQuery request, CancellationToken cancellationToken)
    {
        var cuisineList = !string.IsNullOrWhiteSpace(request.Cuisines)
            ? request.Cuisines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var dietaryList = !string.IsNullOrWhiteSpace(request.Dietary)
            ? request.Dietary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var restaurants = new List<RestaurantCardDto>();
        var dishes = new List<DishCardDto>();
        var totalCount = 0;

        if (request.Type is "restaurants" or "all")
        {
            var (items, count) = await SearchRestaurants(request, cuisineList, cancellationToken);
            restaurants = items;
            totalCount += count;
        }

        if (request.Type is "dishes" or "all")
        {
            var (items, count) = await SearchDishes(request, cuisineList, dietaryList, cancellationToken);
            dishes = items;
            totalCount += count;
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize);

        return new SearchResultDto
        {
            Type = request.Type,
            Restaurants = restaurants,
            Dishes = dishes,
            Pagination = new PaginationInfo
            {
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            },
            AppliedFilters = new AppliedFiltersDto
            {
                Type = request.Type,
                Cuisines = cuisineList,
                Dietary = dietaryList,
                GeoEnabled = request.Lat.HasValue && request.Lng.HasValue
            }
        };
    }

    private async Task<(List<RestaurantCardDto> Items, int Count)> SearchRestaurants(
        SearchQuery request,
        List<string> cuisineList,
        CancellationToken cancellationToken)
    {
        var query = _db.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Where(r => r.Status == RestaurantStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
            query = query.Where(r => EF.Functions.Like(r.RestaurantName.ToLower(), $"%{request.Query.ToLower()}%"));

        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(r => r.City != null && EF.Functions.Like(r.City.CityName.ToLower(), $"%{request.Location.ToLower()}%"));

        if (cuisineList.Count > 0)
            query = query.Where(r => r.CuisineType != null && cuisineList.Contains(r.CuisineType));

        if (request.MinPrice.HasValue)
            query = query.Where(r => r.PriceLevel >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(r => r.PriceLevel <= request.MaxPrice.Value);

        query = (request.SortBy.ToLowerInvariant(), request.SortDir.ToLowerInvariant()) switch
        {
            ("name", "asc") => query.OrderBy(r => r.RestaurantName),
            ("name", _) => query.OrderByDescending(r => r.RestaurantName),
            ("price", "asc") => query.OrderBy(r => r.PriceLevel),
            ("price", _) => query.OrderByDescending(r => r.PriceLevel),
            ("trending", _) => query.OrderByDescending(r => r.TrendingScore),
            (_, "asc") => query.OrderBy(r => r.AvgFoodScore),
            _ => query.OrderByDescending(r => r.AvgFoodScore)
        };

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new RestaurantCardDto
            {
                PublicId = r.PublicId,
                Slug = r.Slug ?? string.Empty,
                RestaurantName = r.RestaurantName,
                CuisineType = r.CuisineType,
                CityName = r.City != null ? r.City.CityName : null,
                PriceLevel = r.PriceLevel,
                AvgFoodScore = r.AvgFoodScore,
                ReviewCount = 0,
                ImageUrl = r.ImageUrl,
                ImageBlurhash = r.ImageBlurhash,
                IsFavorite = false
            })
            .ToListAsync(cancellationToken);

        return (items, count);
    }

    private async Task<(List<DishCardDto> Items, int Count)> SearchDishes(
        SearchQuery request,
        List<string> cuisineList,
        List<string> dietaryList,
        CancellationToken cancellationToken)
    {
        var query = _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
                .ThenInclude(r => r!.City)
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
            query = query.Where(d => EF.Functions.Like(d.DishName.ToLower(), $"%{request.Query.ToLower()}%"));

        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(d => d.Restaurant!.City != null &&
                EF.Functions.Like(d.Restaurant.City!.CityName.ToLower(), $"%{request.Location.ToLower()}%"));

        if (cuisineList.Count > 0)
            query = query.Where(d => d.Restaurant!.CuisineType != null &&
                cuisineList.Contains(d.Restaurant.CuisineType));

        if (request.MinPrice.HasValue)
            query = query.Where(d => d.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(d => d.Price <= request.MaxPrice.Value);

        foreach (var dietary in dietaryList)
        {
            query = dietary.ToLowerInvariant() switch
            {
                "vegetarian" => query.Where(d => d.IsVegetarian),
                "vegan" => query.Where(d => d.IsVegan),
                "gluten_free" or "glutenfree" => query.Where(d => d.IsGlutenFree),
                "lactose_free" or "lactosefree" => query.Where(d => d.IsLactoseFree),
                _ => query
            };
        }

        query = (request.SortBy.ToLowerInvariant(), request.SortDir.ToLowerInvariant()) switch
        {
            ("name", "asc") => query.OrderBy(d => d.DishName),
            ("name", _) => query.OrderByDescending(d => d.DishName),
            ("price", "asc") => query.OrderBy(d => d.Price),
            ("price", _) => query.OrderByDescending(d => d.Price),
            ("trending", _) => query.OrderByDescending(d => d.TrendingScore),
            (_, "asc") => query.OrderBy(d => d.AvgRating),
            _ => query.OrderByDescending(d => d.AvgRating)
        };

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
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
                IsSaved = false
            })
            .ToListAsync(cancellationToken);

        return (items, count);
    }
}
