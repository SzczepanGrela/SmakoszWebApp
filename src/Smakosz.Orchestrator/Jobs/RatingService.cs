using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class RatingService
{
    private readonly ISmakoszDbContext _db;
    private readonly ILogger<RatingService> _logger;

    public RatingService(ISmakoszDbContext db, ILogger<RatingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpdateAsync(CancellationToken ct)
    {
        var restaurantAverages = await _db.Reviews
            .GroupBy(r => r.RestaurantId)
            .Select(g => new
            {
                RestaurantId = g.Key,
                AvgFood = g.Average(r => (double)r.DishRating),
                AvgService = g.Average(r => (double)r.ServiceRating),
                AvgCleanliness = g.Average(r => (double)r.CleanlinessRating),
                AvgAmbiance = g.Average(r => (double)r.AmbianceRating)
            })
            .ToListAsync(ct);

        var restaurantIds = restaurantAverages.Select(a => a.RestaurantId).ToList();
        var restaurants = await _db.Restaurants
            .Where(r => restaurantIds.Contains(r.RestaurantId))
            .ToListAsync(ct);

        var restaurantMap = restaurantAverages.ToDictionary(a => a.RestaurantId);
        foreach (var restaurant in restaurants)
        {
            if (restaurantMap.TryGetValue(restaurant.RestaurantId, out var avg))
            {
                restaurant.AvgFoodScore = avg.AvgFood;
                restaurant.AvgService = avg.AvgService;
                restaurant.AvgCleanliness = avg.AvgCleanliness;
                restaurant.AvgAmbiance = avg.AvgAmbiance;
            }
        }

        var dishAverages = await _db.Reviews
            .GroupBy(r => r.DishId)
            .Select(g => new
            {
                DishId = g.Key,
                AvgRating = g.Average(r => (double)r.DishRating),
                Count = g.Count()
            })
            .ToListAsync(ct);

        var dishIds = dishAverages.Select(a => a.DishId).ToList();
        var dishes = await _db.Dishes
            .Where(d => dishIds.Contains(d.DishId))
            .ToListAsync(ct);

        var dishMap = dishAverages.ToDictionary(a => a.DishId);
        foreach (var dish in dishes)
        {
            if (dishMap.TryGetValue(dish.DishId, out var avg))
            {
                dish.AvgRating = avg.AvgRating;
                dish.ReviewCount = avg.Count;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "avg-ratings: updated {Restaurants} restaurants, {Dishes} dishes",
            restaurantAverages.Count, dishAverages.Count);
    }
}
