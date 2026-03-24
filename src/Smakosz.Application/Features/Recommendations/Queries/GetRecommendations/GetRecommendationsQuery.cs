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

    public GetRecommendationsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<RecommendationsDto>> Handle(GetRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Dishes.AsNoTracking()
            .Where(d => d.IsAvailable && d.Restaurant != null);

        // For authenticated users, exclude dishes they already reviewed
        if (_currentUser.UserId.HasValue)
        {
            var reviewedDishIds = _db.Reviews
                .Where(r => r.UserId == _currentUser.UserId.Value && !r.IsDeleted)
                .Select(r => r.DishId);

            query = query.Where(d => !reviewedDishIds.Contains(d.DishId));
        }

        var trending = await query
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

        return new RecommendationsDto
        {
            NcfAvailable = false,
            Trending = trending,
            Personalized = new List<RecommendedDishDto>(),
            FallbackReason = "Model NCF nie został jeszcze wytrenowany. Pokazujemy popularne dania."
        };
    }
}
