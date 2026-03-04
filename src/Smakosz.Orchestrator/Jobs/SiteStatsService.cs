using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class SiteStatsService
{
    private readonly ISmakoszDbContext _db;
    private readonly ILogger<SiteStatsService> _logger;

    public SiteStatsService(ISmakoszDbContext db, ILogger<SiteStatsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpdateAsync(CancellationToken ct)
    {
        var stats = await _db.SiteStats.FirstAsync(ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var monthAgo = DateTime.UtcNow.AddDays(-30);

        stats.TotalDishes = await _db.Dishes.CountAsync(ct);
        stats.TotalRestaurants = await _db.Restaurants
            .CountAsync(r => r.Status == RestaurantStatus.Active, ct);
        stats.TotalReviews = await _db.Reviews
            .CountAsync(r => !r.IsDeleted, ct);
        stats.TotalUsers = await _db.Users
            .CountAsync(u => u.IsActive && !u.IsDeleted, ct);
        stats.TotalPhotos = await _db.MediaAssets
            .CountAsync(m => m.Status == MediaAssetStatus.Approved, ct);

        stats.ReviewsThisWeek = await _db.Reviews
            .CountAsync(r => !r.IsDeleted && r.CreatedAt >= weekAgo, ct);
        stats.NewUsersThisMonth = await _db.Users
            .CountAsync(u => u.CreatedAt >= monthAgo, ct);

        var dishRatingQuery = _db.Dishes.Where(d => d.AvgRating.HasValue);
        stats.AvgDishRating = await dishRatingQuery.AnyAsync(ct)
            ? await dishRatingQuery.AverageAsync(d => d.AvgRating!.Value, ct)
            : 0;

        var restaurantScoreQuery = _db.Restaurants.Where(r => r.AvgFoodScore.HasValue);
        stats.AvgRestaurantFoodScore = await restaurantScoreQuery.AnyAsync(ct)
            ? await restaurantScoreQuery.AverageAsync(r => r.AvgFoodScore!.Value, ct)
            : 0;

        stats.MostPopularCuisine = await _db.Restaurants
            .Where(r => r.Status == RestaurantStatus.Active && r.CuisineType != null)
            .GroupBy(r => r.CuisineType!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        stats.MostActiveCity = await _db.Reviews
            .Where(r => !r.IsDeleted && r.Restaurant != null && r.Restaurant.City != null)
            .GroupBy(r => r.Restaurant!.City!.CityName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        stats.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "site-stats: dishes={Dishes}, restaurants={Restaurants}, reviews={Reviews}, users={Users}",
            stats.TotalDishes, stats.TotalRestaurants, stats.TotalReviews, stats.TotalUsers);
    }
}
