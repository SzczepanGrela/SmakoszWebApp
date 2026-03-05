using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Recommendations.Queries.GetRecommendations;

public record GetRecommendationsQuery() : IRequest<ErrorOr<RecommendationsDto>>;

public class RecommendationsDto
{
    public bool NcfAvailable { get; set; }
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

        // For authenticated users, get reviewed dish IDs for exclusion
        HashSet<int>? reviewedDishIds = null;
        if (_currentUser.UserId.HasValue)
        {
            reviewedDishIds = (await _db.Reviews
                .Where(r => r.UserId == _currentUser.UserId.Value && !r.IsDeleted)
                .Select(r => r.DishId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        // Always fetch trending
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

        // Try personalized recommendations
        if (_currentUser.UserId.HasValue && _provider.IsAvailable)
        {
            // Check minimum reviews (cold start)
            var minReviewsConfig = await _db.SystemConfigs
                .FirstOrDefaultAsync(c => c.Key == "min_reviews_for_recommendations", cancellationToken);
            var minReviews = minReviewsConfig is not null
                ? int.TryParse(minReviewsConfig.Value, out var mr) ? mr : 10
                : 10;

            var userReviewCount = reviewedDishIds?.Count
                ?? await _db.Reviews
                    .CountAsync(r => r.UserId == _currentUser.UserId.Value && !r.IsDeleted, cancellationToken);

            if (userReviewCount < minReviews)
            {
                var remaining = minReviews - userReviewCount;
                result.FallbackReason = $"Wystaw jeszcze {remaining} recenzji, aby otrzymać personalizowane rekomendacje.";
            }
            else
            {
                try
                {
                    var personalized = await _provider.GetPersonalizedAsync(
                        _currentUser.UserId.Value, 12, cancellationToken);

                    // Filter out already reviewed dishes
                    if (reviewedDishIds is not null)
                        personalized = personalized.Where(p => !reviewedDishIds.Contains(p.DishId)).ToList();

                    if (personalized.Count > 0)
                    {
                        var personalizedDishIds = personalized.Select(p => p.DishId).ToList();
                        var scoreMap = personalized.ToDictionary(p => p.DishId, p => p.Score);

                        var dishes = await _db.Dishes.AsNoTracking()
                            .Where(d => personalizedDishIds.Contains(d.DishId) && d.IsAvailable && d.Restaurant != null)
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

                        // Apply NCF scores and re-sort
                        foreach (var dish in dishes)
                        {
                            if (scoreMap.TryGetValue(dish.DishId, out var score))
                                dish.Score = (decimal)score;
                        }

                        result.Personalized = dishes.OrderByDescending(d => d.Score).ToList();
                        result.NcfAvailable = true;
                    }
                }
                catch (Exception)
                {
                    result.FallbackReason = "Wystąpił błąd podczas generowania rekomendacji.";
                }
            }
        }
        else if (!_provider.IsAvailable)
        {
            result.FallbackReason = _provider.FallbackReason;
        }

        return result;
    }
}
