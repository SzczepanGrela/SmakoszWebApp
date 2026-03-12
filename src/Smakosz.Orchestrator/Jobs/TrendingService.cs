using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class TrendingService
{
    private readonly ISmakoszDbContext _db;
    private readonly ILogger<TrendingService> _logger;

    public TrendingService(ISmakoszDbContext db, ILogger<TrendingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecalculateAsync(CancellationToken ct)
    {
        var globalStats = await _db.Reviews
            .GroupBy(_ => 1)
            .Select(g => new
            {
                C = (double)g.Count() / Math.Max(1,
                    g.Select(r => r.RestaurantId).Distinct().Count()),
                M = g.Average(r => (double)r.DishRating)
            })
            .FirstOrDefaultAsync(ct);

        if (globalStats is null)
        {
            _logger.LogInformation("trending-scores: no reviews found, skipping");
            return;
        }

        double c = globalStats.C;
        double m = globalStats.M;

        var restaurantStats = await _db.Reviews
            .GroupBy(r => r.RestaurantId)
            .Select(g => new
            {
                RestaurantId = g.Key,
                N = g.Count(),
                Sum = g.Sum(r => (double)r.DishRating)
            })
            .ToListAsync(ct);

        var restaurantIds = restaurantStats.Select(s => s.RestaurantId).ToList();
        var restaurants = await _db.Restaurants
            .Where(r => restaurantIds.Contains(r.RestaurantId))
            .ToListAsync(ct);

        var rMap = restaurantStats.ToDictionary(s => s.RestaurantId);
        foreach (var restaurant in restaurants)
        {
            if (rMap.TryGetValue(restaurant.RestaurantId, out var stats))
            {
                restaurant.TrendingScore = (decimal)((c * m + stats.Sum) / (c + stats.N));
            }
        }

        var dishStats = await _db.Reviews
            .GroupBy(r => r.DishId)
            .Select(g => new
            {
                DishId = g.Key,
                N = g.Count(),
                Sum = g.Sum(r => (double)r.DishRating)
            })
            .ToListAsync(ct);

        var dishIds = dishStats.Select(s => s.DishId).ToList();
        var dishes = await _db.Dishes
            .Where(d => dishIds.Contains(d.DishId))
            .ToListAsync(ct);

        var dMap = dishStats.ToDictionary(s => s.DishId);
        foreach (var dish in dishes)
        {
            if (dMap.TryGetValue(dish.DishId, out var stats))
            {
                dish.TrendingScore = (decimal)((c * m + stats.Sum) / (c + stats.N));
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "trending-scores: updated {Restaurants} restaurants, {Dishes} dishes (C={C:F2}, m={M:F2})",
            restaurantStats.Count, dishStats.Count, c, m);
    }
}
