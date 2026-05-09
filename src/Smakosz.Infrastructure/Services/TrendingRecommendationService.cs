using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class TrendingRecommendationService : IRecommendationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrendingRecommendationService> _logger;

    public TrendingRecommendationService(
        IServiceScopeFactory scopeFactory,
        ILogger<TrendingRecommendationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsAvailable => true;
    public bool IsUserInMapping(int userId) => false;
    public string GetLoadedVersion() => string.Empty;
    public IReadOnlyList<int> GetMappedUserIds() => [];

    public string? FallbackReason =>
        "Model NCF nie jest jeszcze dostępny. Pokazujemy popularne dania.";

    public async Task<List<(int DishId, float Score)>> GetPersonalizedAsync(
        int userId, int count, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();

        var trending = await db.Dishes.AsNoTracking()
            .Where(d => d.IsAvailable && d.Restaurant != null)
            .OrderByDescending(d => d.TrendingScore)
            .ThenByDescending(d => d.AvgRating)
            .Take(count)
            .Select(d => new { d.DishId, d.TrendingScore, d.AvgRating })
            .ToListAsync(ct);

        return trending
            .Select(d => (d.DishId, (float)(d.TrendingScore ?? (decimal?)d.AvgRating ?? 0)))
            .ToList();
    }
}
