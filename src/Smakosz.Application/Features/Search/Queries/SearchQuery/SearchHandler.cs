using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Search.Dtos;
using Smakosz.Domain.Constants;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Search.Queries.SearchQuery;

public class SearchHandler : IRequestHandler<SearchQuery, ErrorOr<SearchResultDto>>
{
    private const string SimilarityThresholdKey = "search.fullsearch.similarity_threshold";
    private const double DefaultSimilarityThreshold = 0.3;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublicConfigProvider _config;

    public SearchHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IPublicConfigProvider config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ErrorOr<SearchResultDto>> Handle(SearchQuery request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            _db.SearchHistories.Add(new SearchHistory
            {
                UserId = _currentUser.UserId,
                SearchQuery = request.Query,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        var similarityThreshold = await _config.GetDoubleAsync(SimilarityThresholdKey, DefaultSimilarityThreshold, cancellationToken);

        var cuisineList = !string.IsNullOrWhiteSpace(request.Cuisines)
            ? request.Cuisines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var dietaryList = !string.IsNullOrWhiteSpace(request.Dietary)
            ? request.Dietary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var tagList = !string.IsNullOrWhiteSpace(request.Tags)
            ? request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var dishCategoryList = !string.IsNullOrWhiteSpace(request.DishCategories)
            ? request.DishCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var featureList = ParseCsv(request.Features);
        var moodList = ParseCsv(request.Moods);
        var occasionList = ParseCsv(request.Occasions);
        var spiceList = ParseCsv(request.SpiceLevels);

        var restaurants = new List<RestaurantCardDto>();
        var dishes = new List<DishCardDto>();
        var totalCount = 0;

        if (request.Type is "restaurants" or "all")
        {
            var (items, count) = await SearchRestaurants(request, cuisineList, tagList, similarityThreshold, cancellationToken);
            restaurants = items;
            totalCount += count;
        }

        if (request.Type is "dishes" or "all")
        {
            var (items, count) = await SearchDishes(request, cuisineList, dietaryList, tagList, dishCategoryList, featureList, moodList, occasionList, spiceList, similarityThreshold, cancellationToken);
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
                Dietary = dietaryList
            }
        };
    }

    private async Task<(List<RestaurantCardDto> Items, int Count)> SearchRestaurants(
        SearchQuery request,
        List<string> cuisineList,
        List<string> tagList,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        var query = _db.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Include(r => r.Cuisine)
            .Where(r => r.Status == RestaurantStatus.Active)
            .AsQueryable();

        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);
        var term = hasQuery ? request.Query!.ToLower() : string.Empty;

        if (hasQuery)
        {
            query = query.Where(r =>
                EF.Functions.TrigramsWordSimilarity(term, r.RestaurantName.ToLower()) > similarityThreshold ||
                (r.Description != null && EF.Functions.TrigramsWordSimilarity(term, r.Description.ToLower()) > similarityThreshold) ||
                (r.Cuisine != null && EF.Functions.TrigramsWordSimilarity(term, r.Cuisine.DisplayName.ToLower()) > similarityThreshold) ||
                _db.RestaurantTags.Any(rt => rt.RestaurantId == r.RestaurantId && EF.Functions.TrigramsWordSimilarity(term, rt.Tag.TagName.ToLower()) > similarityThreshold));
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(r => r.City != null && EF.Functions.ILike(r.City.CityName, $"%{request.Location}%"));

        if (cuisineList.Count > 0)
        {
            var lower = cuisineList.Select(c => c.ToLower().Replace("_", " ")).ToList();
            query = query.Where(r => r.Cuisine != null && lower.Contains(r.Cuisine.Name.ToLower()));
        }

        if (tagList.Count > 0)
            query = query.Where(r => _db.RestaurantTags.Any(rt => rt.RestaurantId == r.RestaurantId && tagList.Contains(rt.Tag.TagName)));

        if (request.MinPrice.HasValue)
            query = query.Where(r => r.PriceLevel >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(r => r.PriceLevel <= request.MaxPrice.Value);

        // When the user gave a query and did not override sort, rank by trigram similarity so the most relevant matches surface first; popularity becomes the tiebreak. Explicit sort modes (name/price/trending) bypass relevance.
        var (sortBy, sortDir) = (request.SortBy.ToLowerInvariant(), request.SortDir.ToLowerInvariant());
        query = sortBy switch
        {
            "name" when sortDir == "asc" => query.OrderBy(r => r.RestaurantName),
            "name" => query.OrderByDescending(r => r.RestaurantName),
            "price" when sortDir == "asc" => query.OrderBy(r => r.PriceLevel),
            "price" => query.OrderByDescending(r => r.PriceLevel),
            "trending" => query.OrderByDescending(r => r.TrendingScore),
            _ when hasQuery => query
                .OrderByDescending(r => EF.Functions.TrigramsWordSimilarity(term, r.RestaurantName.ToLower()))
                .ThenByDescending(r => r.AvgFoodScore),
            _ when sortDir == "asc" => query.OrderBy(r => r.AvgFoodScore),
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
                CuisineType = r.Cuisine != null ? r.Cuisine.DisplayName : null,
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
        List<string> tagList,
        List<string> dishCategoryList,
        List<string> featureList,
        List<string> moodList,
        List<string> occasionList,
        List<string> spiceList,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        var query = _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
                .ThenInclude(r => r!.City)
            .Include(d => d.Restaurant)
                .ThenInclude(r => r!.Cuisine)
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active)
            .AsQueryable();

        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);
        var term = hasQuery ? request.Query!.ToLower() : string.Empty;

        if (hasQuery)
        {
            query = query.Where(d =>
                EF.Functions.TrigramsWordSimilarity(term, d.DishName.ToLower()) > similarityThreshold ||
                (d.Description != null && EF.Functions.TrigramsWordSimilarity(term, d.Description.ToLower()) > similarityThreshold) ||
                (d.Restaurant!.Cuisine != null && EF.Functions.TrigramsWordSimilarity(term, d.Restaurant.Cuisine.DisplayName.ToLower()) > similarityThreshold) ||
                d.DishTags.Any(dt => EF.Functions.TrigramsWordSimilarity(term, dt.Tag.TagName.ToLower()) > similarityThreshold));
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(d => d.Restaurant!.City != null &&
                EF.Functions.ILike(d.Restaurant.City!.CityName, $"%{request.Location}%"));

        if (cuisineList.Count > 0)
        {
            var lower = cuisineList.Select(c => c.ToLower().Replace("_", " ")).ToList();
            query = query.Where(d => d.Restaurant!.Cuisine != null &&
                lower.Contains(d.Restaurant.Cuisine.Name.ToLower()));
        }

        if (tagList.Count > 0)
            query = query.Where(d => d.DishTags.Any(dt => tagList.Contains(dt.Tag.TagName)));

        if (dishCategoryList.Count > 0)
        {
            query = query.Where(d => d.DishTags.Any(dt =>
                dt.Tag.Category == TagCategories.DishCategory
                && dishCategoryList.Contains(dt.Tag.TagName)));
        }

        if (featureList.Count > 0)
        {
            query = query.Where(d => d.DishTags.Any(dt =>
                dt.Tag.Category == TagCategories.Feature
                && featureList.Contains(dt.Tag.TagName)));
        }

        if (moodList.Count > 0)
        {
            query = query.Where(d => d.DishTags.Any(dt =>
                dt.Tag.Category == TagCategories.Mood
                && moodList.Contains(dt.Tag.TagName)));
        }

        if (occasionList.Count > 0)
        {
            query = query.Where(d => d.DishTags.Any(dt =>
                dt.Tag.Category == TagCategories.Occasion
                && occasionList.Contains(dt.Tag.TagName)));
        }

        if (spiceList.Count > 0)
        {
            query = query.Where(d => d.DishTags.Any(dt =>
                dt.Tag.Category == TagCategories.Spice
                && spiceList.Contains(dt.Tag.TagName)));
        }

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

        var (sortBy, sortDir) = (request.SortBy.ToLowerInvariant(), request.SortDir.ToLowerInvariant());
        query = sortBy switch
        {
            "name" when sortDir == "asc" => query.OrderBy(d => d.DishName),
            "name" => query.OrderByDescending(d => d.DishName),
            "price" when sortDir == "asc" => query.OrderBy(d => d.Price),
            "price" => query.OrderByDescending(d => d.Price),
            "trending" => query.OrderByDescending(d => d.TrendingScore),
            _ when hasQuery => query
                .OrderByDescending(d => EF.Functions.TrigramsWordSimilarity(term, d.DishName.ToLower()))
                .ThenByDescending(d => d.AvgRating),
            _ when sortDir == "asc" => query.OrderBy(d => d.AvgRating),
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
                SpiceLevel = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Spice)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                Mood = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Mood)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                Features = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Feature)
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                Occasions = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Occasion)
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                IsSaved = false,
                CategoryTagName = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.DishCategory)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                CategoryColor = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.DishCategory)
                    .Select(dt => dt.Tag.DisplayColor)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return (items, count);
    }

    private static List<string> ParseCsv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
