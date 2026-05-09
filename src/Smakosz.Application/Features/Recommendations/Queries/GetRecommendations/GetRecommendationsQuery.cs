using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

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
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRecommendationProvider _provider;

    public GetRecommendationsHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IRecommendationProvider provider)
    {
        _db = db;
        _currentUser = currentUser;
        _provider = provider;
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

        if (_currentUser.UserId.HasValue && _provider.IsAvailable)
        {
            var ncfEnabled = await _db.SystemConfigs
                .Where(c => c.Key == "ncf.available")
                .Select(c => c.Value)
                .FirstOrDefaultAsync(cancellationToken);

            if (ncfEnabled == "false")
            {
                result.FallbackReason = "System rekomendacji jest tymczasowo wyłączony.";
            }
            else if (!_provider.IsUserInMapping(_currentUser.UserId.Value))
            {
                result.IsNewcomer = true;
                result.FallbackReason = "Wystawiłeś za mało recenzji. Wystaw więcej i spróbuj ponownie jutro.";
            }
            else
            {
                var cacheRow = await _db.UserRecommendationCaches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId.Value, cancellationToken);

                if (cacheRow is null)
                {
                    result.FallbackReason = "Rekomendacje są właśnie generowane. Sprawdź ponownie za chwilę.";
                }
                else
                {
                    var cached = JsonSerializer.Deserialize<List<CachedDishEntry>>(cacheRow.TopDishIdsJson) ?? [];
                    var unreviewed = reviewedDishIds is null
                        ? cached
                        : cached.Where(c => !reviewedDishIds.Contains(c.DishId)).ToList();

                    if (unreviewed.Count > 0)
                    {
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
                    }
                }
            }
        }
        else if (!_provider.IsAvailable)
        {
            result.FallbackReason = _provider.FallbackReason;
        }

        return result;
    }

    private sealed record CachedDishEntry(
        [property: JsonPropertyName("dishId")] int DishId,
        [property: JsonPropertyName("score")] float Score);
}
