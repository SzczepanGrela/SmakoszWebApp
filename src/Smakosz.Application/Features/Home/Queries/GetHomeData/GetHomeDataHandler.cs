using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Home.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Home.Queries.GetHomeData;

public class GetHomeDataHandler : IRequestHandler<GetHomeDataQuery, ErrorOr<HomeDataDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ISmakoszDbContextFactory _dbFactory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GetHomeDataHandler(ISmakoszDbContext db, ISmakoszDbContextFactory dbFactory)
    {
        _db = db;
        _dbFactory = dbFactory;
    }

    public async Task<ErrorOr<HomeDataDto>> Handle(GetHomeDataQuery request, CancellationToken cancellationToken)
    {
        var cache = await _db.HomePageCaches.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var stats = new StatsDto
        {
            TotalDishes = cache?.TotalDishes ?? 0,
            TotalRestaurants = cache?.TotalRestaurants ?? 0,
            TotalReviews = cache?.TotalReviews ?? 0
        };

        var hasCachedData = cache is not null
            && cache.TrendingRestaurantsJson is not null
            && cache.TrendingDishesJson is not null
            && cache.TopRatedDishesJson is not null
            && cache.RecentReviewsJson is not null
            && cache.PopularCategoriesJson is not null
            && cache.NewestRestaurantsJson is not null
            && cache.MostReviewedDishesJson is not null;

        List<RestaurantCardDto> trendingRestaurants;
        List<DishCardDto> trendingDishes;
        List<DishCardDto> topRatedDishes;
        List<ReviewCardDto> recentReviews;
        List<PopularCategoryDto> popularCategories;
        HeroImageDto? heroImage;
        List<RestaurantCardDto> newestRestaurants;
        List<DishCardDto> mostReviewedDishes;

        if (hasCachedData && TryDeserializePopularCategories(cache!.PopularCategoriesJson!, out var cachedCategories))
        {
            trendingRestaurants = JsonSerializer.Deserialize<List<RestaurantCardDto>>(cache.TrendingRestaurantsJson!, JsonOpts) ?? [];
            trendingDishes = JsonSerializer.Deserialize<List<DishCardDto>>(cache.TrendingDishesJson!, JsonOpts) ?? [];
            topRatedDishes = JsonSerializer.Deserialize<List<DishCardDto>>(cache.TopRatedDishesJson!, JsonOpts) ?? [];
            recentReviews = JsonSerializer.Deserialize<List<ReviewCardDto>>(cache.RecentReviewsJson!, JsonOpts) ?? [];
            popularCategories = cachedCategories;
            heroImage = cache.HeroImageJson is not null
                ? JsonSerializer.Deserialize<HeroImageDto>(cache.HeroImageJson, JsonOpts)
                : null;
            newestRestaurants = JsonSerializer.Deserialize<List<RestaurantCardDto>>(cache.NewestRestaurantsJson!, JsonOpts) ?? [];
            mostReviewedDishes = JsonSerializer.Deserialize<List<DishCardDto>>(cache.MostReviewedDishesJson!, JsonOpts) ?? [];
        }
        else
        {
            var trendingRestaurantsTask = QueryTrendingRestaurantsParallel(cancellationToken);
            var trendingDishesTask = QueryTrendingDishesParallel(cancellationToken);
            var topRatedDishesTask = QueryTopRatedDishesParallel(cancellationToken);
            var recentReviewsTask = QueryRecentReviewsParallel(cancellationToken);
            var popularCategoriesTask = QueryPopularCategoriesParallel(cancellationToken);
            var heroImageTask = QueryHeroImageParallel(cancellationToken);
            var newestRestaurantsTask = QueryNewestRestaurantsParallel(cancellationToken);
            var mostReviewedDishesTask = QueryMostReviewedDishesParallel(cancellationToken);

            await Task.WhenAll(
                trendingRestaurantsTask, trendingDishesTask, topRatedDishesTask,
                recentReviewsTask, popularCategoriesTask, heroImageTask,
                newestRestaurantsTask, mostReviewedDishesTask);

            trendingRestaurants = await trendingRestaurantsTask;
            trendingDishes = await trendingDishesTask;
            topRatedDishes = await topRatedDishesTask;
            recentReviews = await recentReviewsTask;
            popularCategories = await popularCategoriesTask;
            heroImage = await heroImageTask;
            newestRestaurants = await newestRestaurantsTask;
            mostReviewedDishes = await mostReviewedDishesTask;
        }

        return new HomeDataDto
        {
            Stats = stats,
            TrendingRestaurants = trendingRestaurants,
            TrendingDishes = trendingDishes,
            TopRatedDishes = topRatedDishes,
            RecentReviews = recentReviews,
            PopularCategories = popularCategories,
            HeroImage = heroImage,
            NewestRestaurants = newestRestaurants,
            MostReviewedDishes = mostReviewedDishes
        };
    }

    private async Task<List<RestaurantCardDto>> QueryTrendingRestaurantsParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Include(r => r.Cuisine)
            .Where(r => r.Status == RestaurantStatus.Active
                && (r.ModerationStatus == ContentModerationStatus.None || r.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(r => r.TrendingScore)
            .Take(6)
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
            .ToListAsync(ct);
    }

    private async Task<List<DishCardDto>> QueryTrendingDishesParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active
                && (d.ModerationStatus == ContentModerationStatus.None || d.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(d => d.TrendingScore)
            .Take(12)
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
            .ToListAsync(ct);
    }

    private async Task<List<DishCardDto>> QueryTopRatedDishesParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.ReviewCount >= 3 && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active
                && (d.ModerationStatus == ContentModerationStatus.None || d.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(d => d.AvgRating)
            .Take(12)
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
            .ToListAsync(ct);
    }

    private async Task<List<ReviewCardDto>> QueryRecentReviewsParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Dish)
            .Include(r => r.Restaurant)
            .Where(r => !r.IsDeleted && r.IsVisible
                && (r.ModerationStatus == ContentModerationStatus.None || r.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new ReviewCardDto
            {
                PublicId = r.PublicId,
                DishRating = r.DishRating,
                ServiceRating = r.ServiceRating,
                CleanlinessRating = r.CleanlinessRating,
                AmbianceRating = r.AmbianceRating,
                Content = r.Content,
                ContentStatus = r.ModerationStatus,
                VisitDate = r.VisitDate,
                HelpfulCount = r.HelpfulCount,
                IsHelpfulByMe = false,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Author = new UserSummaryDto
                {
                    PublicId = r.User.PublicId,
                    Slug = r.User.Slug ?? string.Empty,
                    Username = r.User.Username,
                    AvatarUrl = r.User.AvatarUrl,
                    AvatarBlurhash = r.User.AvatarBlurhash,
                    ReviewCount = r.User.ReviewCount
                },
                DishName = r.Dish.DishName,
                DishSlug = r.Dish.Slug ?? string.Empty,
                RestaurantName = r.Restaurant.RestaurantName,
                RestaurantSlug = r.Restaurant.Slug ?? string.Empty
            })
            .ToListAsync(ct);
    }

    private async Task<List<PopularCategoryDto>> QueryPopularCategoriesParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Restaurants
            .AsNoTracking()
            .Where(r => r.Status == RestaurantStatus.Active && r.Cuisine != null)
            .GroupBy(r => new { r.Cuisine!.DisplayName, r.Cuisine.Name, r.Cuisine.Icon })
            .OrderByDescending(g => g.Count())
            .Take(7)
            .Select(g => new PopularCategoryDto { Name = g.Key.DisplayName, Slug = g.Key.Name, Icon = g.Key.Icon })
            .ToListAsync(ct);
    }

    private static bool TryDeserializePopularCategories(string json, out List<PopularCategoryDto> result)
    {
        try
        {
            result = JsonSerializer.Deserialize<List<PopularCategoryDto>>(json, JsonOpts) ?? [];
            return result.Count == 0
                || (!string.IsNullOrEmpty(result[0].Name) && !string.IsNullOrEmpty(result[0].Slug));
        }
        catch (JsonException)
        {
            result = [];
            return false;
        }
    }

    private async Task<HeroImageDto?> QueryHeroImageParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.MediaAssets
            .AsNoTracking()
            .Where(m => m.EntityType == MediaEntityType.Hero && m.ModerationStatus == ContentModerationStatus.Approved)
            .OrderBy(_ => EF.Functions.Random())
            .Select(m => new HeroImageDto { Url = m.Url, Blurhash = m.Blurhash, CreditText = m.CreditText })
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<RestaurantCardDto>> QueryNewestRestaurantsParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Restaurants
            .AsNoTracking()
            .Include(r => r.City)
            .Include(r => r.Cuisine)
            .Where(r => r.Status == RestaurantStatus.Active
                && (r.ModerationStatus == ContentModerationStatus.None || r.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(r => r.CreatedAt)
            .Take(6)
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
            .ToListAsync(ct);
    }

    private async Task<List<DishCardDto>> QueryMostReviewedDishesParallel(CancellationToken ct)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync(ct);
        return await ctx.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Where(d => d.IsAvailable && d.ReviewCount >= 5
                && d.Restaurant != null && d.Restaurant.Status == RestaurantStatus.Active
                && (d.ModerationStatus == ContentModerationStatus.None || d.ModerationStatus == ContentModerationStatus.Approved))
            .OrderByDescending(d => d.ReviewCount)
            .Take(12)
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
            .ToListAsync(ct);
    }
}
