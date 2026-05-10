using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Features.Recommendations.Queries.GetRecommendations;

public record GetRecommendationsQuery() : IRequest<ErrorOr<RecommendationsDto>>;

public class RecommendationsDto
{
    public bool NcfAvailable { get; set; }
    public bool IsNewcomer { get; set; }
    public string? FallbackReason { get; set; }
    public List<RecommendedDishDto> Trending { get; set; } = new();
    public List<RecommendedDishDto> Personalized { get; set; } = new();
}

public class RecommendedDishDto
{
    public int DishId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public string Source { get; set; } = "trending";
    public decimal? Score { get; set; }
}

public class GetRecommendationsHandler : IRequestHandler<GetRecommendationsQuery, ErrorOr<RecommendationsDto>>
{
    private const int CacheSize = 12;
    private const int MinReviewsForPersonalization = 5;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRecommendationProvider _provider;
    private readonly IBusinessMetrics _metrics;
    private readonly ILogger<GetRecommendationsHandler> _logger;

    public GetRecommendationsHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IRecommendationProvider provider,
        IBusinessMetrics metrics,
        ILogger<GetRecommendationsHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _provider = provider;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<ErrorOr<RecommendationsDto>> Handle(GetRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var dishQuery = _db.Dishes.AsNoTracking()
            .Where(d => d.IsAvailable && d.Restaurant != null);

        HashSet<int>? reviewedDishIds = null;
        if (_currentUser.UserId.HasValue)
        {
            reviewedDishIds = (await _db.Reviews
                .Where(r => r.UserId == _currentUser.UserId.Value && !r.IsDeleted)
                .Select(r => r.DishId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var trendingQuery = dishQuery;
        if (reviewedDishIds is not null)
            trendingQuery = trendingQuery.Where(d => !reviewedDishIds.Contains(d.DishId));

        var trending = await trendingQuery
            .OrderByDescending(d => d.TrendingScore)
            .ThenByDescending(d => d.AvgRating)
            .Take(12)
            .Select(d => new RecommendedDishDto
            {
                DishId = d.DishId,
                Name = d.DishName,
                Slug = d.Slug,
                ImageUrl = d.ImageUrl,
                RestaurantName = d.Restaurant!.RestaurantName,
                RestaurantSlug = d.Restaurant.Slug,
                Source = "trending",
                Score = d.TrendingScore ?? (decimal?)d.AvgRating
            })
            .ToListAsync(cancellationToken);

        var result = new RecommendationsDto
        {
            Trending = trending,
            Personalized = new List<RecommendedDishDto>()
        };

        if (!_currentUser.UserId.HasValue)
        {
            _metrics.RecordRecommendationCacheLookup("anonymous");
            return result;
        }

        if (!_provider.IsAvailable)
        {
            result.FallbackReason = _provider.FallbackReason;
            _metrics.RecordRecommendationCacheLookup("provider_unavailable");
            return result;
        }

        var ncfEnabled = await _db.SystemConfigs
            .Where(c => c.Key == "ncf.available")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (ncfEnabled == "false")
        {
            result.FallbackReason = "System rekomendacji jest tymczasowo wyłączony.";
            _metrics.RecordRecommendationCacheLookup("ncf_disabled");
            return result;
        }

        var userId = _currentUser.UserId.Value;
        var userReviewCount = reviewedDishIds?.Count ?? 0;

        if (userReviewCount < MinReviewsForPersonalization || !_provider.IsUserInMapping(userId))
        {
            result.IsNewcomer = true;
            result.FallbackReason = $"Wystaw co najmniej {MinReviewsForPersonalization} recenzji aby dostać spersonalizowane rekomendacje.";
            _metrics.RecordRecommendationCacheLookup("newcomer");
            return result;
        }

        var loadedVersion = _provider.GetLoadedVersion();

        var cacheRow = await _db.UserRecommendationCaches
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        var isHit = cacheRow is not null && cacheRow.ModelVersion == loadedVersion;
        List<CachedDishEntry> cached;

        if (isHit)
        {
            cached = JsonSerializer.Deserialize<List<CachedDishEntry>>(cacheRow!.TopDishIdsJson) ?? [];
        }
        else
        {
            try
            {
                var reviewedCount = reviewedDishIds?.Count ?? 0;
                var top = await _provider.GetPersonalizedAsync(userId, CacheSize + reviewedCount, cancellationToken);

                var filtered = reviewedDishIds is null
                    ? top
                    : top.Where(t => !reviewedDishIds.Contains(t.DishId)).ToList();

                cached = filtered
                    .Take(CacheSize)
                    .Select(t => new CachedDishEntry(t.DishId, t.Score))
                    .ToList();

                var json = JsonSerializer.Serialize(cached);

                if (cacheRow is not null)
                {
                    cacheRow.TopDishIdsJson = json;
                    cacheRow.ModelVersion = loadedVersion;
                    cacheRow.GeneratedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.UserRecommendationCaches.Add(new UserRecommendationCache
                    {
                        UserId = userId,
                        TopDishIdsJson = json,
                        ModelVersion = loadedVersion,
                        GeneratedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lazy recommendation compute failed for user {UserId} version {Version}", userId, loadedVersion);
                result.FallbackReason = "Tymczasowy błąd generowania rekomendacji. Spróbuj ponownie za chwilę.";
                _metrics.RecordRecommendationCacheLookup("compute_failed");
                return result;
            }
        }

        var unreviewed = reviewedDishIds is null
            ? cached
            : cached.Where(c => !reviewedDishIds.Contains(c.DishId)).ToList();

        if (unreviewed.Count == 0)
        {
            _metrics.RecordRecommendationCacheLookup("empty_after_filter");
            return result;
        }

        var dishIds = unreviewed.Select(c => c.DishId).ToList();
        var scoreMap = unreviewed.ToDictionary(c => c.DishId, c => c.Score);

        var dishes = await _db.Dishes.AsNoTracking()
            .Where(d => dishIds.Contains(d.DishId) && d.IsAvailable && d.Restaurant != null)
            .Select(d => new RecommendedDishDto
            {
                DishId = d.DishId,
                Name = d.DishName,
                Slug = d.Slug,
                ImageUrl = d.ImageUrl,
                RestaurantName = d.Restaurant!.RestaurantName,
                RestaurantSlug = d.Restaurant.Slug,
                Source = "ncf"
            })
            .ToListAsync(cancellationToken);

        foreach (var dish in dishes)
        {
            if (scoreMap.TryGetValue(dish.DishId, out var score))
                dish.Score = (decimal)score;
        }

        result.Personalized = dishes.OrderByDescending(d => d.Score).ToList();
        result.NcfAvailable = true;
        _metrics.RecordRecommendationCacheLookup(isHit ? "hit" : "cold_computed");

        return result;
    }

    private sealed record CachedDishEntry(
        [property: JsonPropertyName("dishId")] int DishId,
        [property: JsonPropertyName("score")] float Score);
}
