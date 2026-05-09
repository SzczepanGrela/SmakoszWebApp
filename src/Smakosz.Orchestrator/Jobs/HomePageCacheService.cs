using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Application.Features.Home.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class HomePageCacheService
{
    private readonly ISmakoszDbContext _db;
    private readonly ILogger<HomePageCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HomePageCacheService(ISmakoszDbContext db, ILogger<HomePageCacheService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        var cache = await _db.HomePageCaches.FirstAsync(ct);

        var trendingRestaurants = await _db.Restaurants
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

        var trendingDishes = await _db.Dishes
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

        var topRatedDishes = await _db.Dishes
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

        var recentReviews = await _db.Reviews
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

        var popularCategories = await _db.Restaurants
            .AsNoTracking()
            .Where(r => r.Status == RestaurantStatus.Active && r.Cuisine != null)
            .GroupBy(r => new { r.Cuisine!.DisplayName, r.Cuisine.Name, r.Cuisine.Icon })
            .OrderByDescending(g => g.Count())
            .Take(7)
            .Select(g => new PopularCategoryDto { Name = g.Key.DisplayName, Slug = g.Key.Name, Icon = g.Key.Icon })
            .ToListAsync(ct);

        var heroImage = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.EntityType == MediaEntityType.Hero && m.ModerationStatus == ContentModerationStatus.Approved)
            .OrderBy(_ => EF.Functions.Random())
            .Select(m => new HeroImageDto { Url = m.Url, Blurhash = m.Blurhash, CreditText = m.CreditText })
            .FirstOrDefaultAsync(ct);

        var newestRestaurants = await _db.Restaurants
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

        var mostReviewedDishes = await _db.Dishes
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

        var totalDishes = await _db.Dishes.CountAsync(ct);
        var totalRestaurants = await _db.Restaurants
            .CountAsync(r => r.Status == RestaurantStatus.Active, ct);
        var totalReviews = await _db.Reviews
            .CountAsync(r => !r.IsDeleted, ct);
        var totalUsers = await _db.Users
            .CountAsync(u => u.IsActive && !u.IsDeleted, ct);

        cache.TrendingRestaurantsJson = JsonSerializer.Serialize(trendingRestaurants, JsonOpts);
        cache.TrendingDishesJson = JsonSerializer.Serialize(trendingDishes, JsonOpts);
        cache.TopRatedDishesJson = JsonSerializer.Serialize(topRatedDishes, JsonOpts);
        cache.RecentReviewsJson = JsonSerializer.Serialize(recentReviews, JsonOpts);
        cache.PopularCategoriesJson = JsonSerializer.Serialize(popularCategories, JsonOpts);
        cache.HeroImageJson = heroImage is not null ? JsonSerializer.Serialize(heroImage, JsonOpts) : null;
        cache.NewestRestaurantsJson = JsonSerializer.Serialize(newestRestaurants, JsonOpts);
        cache.MostReviewedDishesJson = JsonSerializer.Serialize(mostReviewedDishes, JsonOpts);
        cache.TotalDishes = totalDishes;
        cache.TotalRestaurants = totalRestaurants;
        cache.TotalReviews = totalReviews;
        cache.TotalUsers = totalUsers;
        cache.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "home-cache: restaurants={Restaurants}, trendingDishes={TDishes}, topDishes={TopDishes}, reviews={Reviews}, categories={Categories}, newest={Newest}, mostReviewed={MostReviewed}, stats(d={D},r={R},rv={Rv},u={U})",
            trendingRestaurants.Count, trendingDishes.Count, topRatedDishes.Count,
            recentReviews.Count, popularCategories.Count,
            newestRestaurants.Count, mostReviewedDishes.Count,
            totalDishes, totalRestaurants, totalReviews, totalUsers);
    }
}
