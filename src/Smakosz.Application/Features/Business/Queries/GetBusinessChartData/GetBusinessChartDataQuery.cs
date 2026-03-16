using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessChartData;

public record GetBusinessChartDataQuery(int Days = 30) : IRequest<ErrorOr<BusinessChartDataDto>>;

public class GetBusinessChartDataHandler : IRequestHandler<GetBusinessChartDataQuery, ErrorOr<BusinessChartDataDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessChartDataHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessChartDataDto>> Handle(GetBusinessChartDataQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var cutoff = DateTime.UtcNow.AddDays(-request.Days);

        var reviews = _db.Reviews
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurant.RestaurantId && !r.IsDeleted);

        var recentReviews = await reviews
            .Where(r => r.CreatedAt >= cutoff)
            .GroupBy(r => r.CreatedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var trendDict = recentReviews.ToDictionary(x => DateOnly.FromDateTime(x.Date), x => x.Count);
        var reviewTrend = new List<DailyReviewCount>();
        for (var day = DateOnly.FromDateTime(cutoff); day <= DateOnly.FromDateTime(DateTime.UtcNow); day = day.AddDays(1))
        {
            reviewTrend.Add(new DailyReviewCount
            {
                Date = day,
                Count = trendDict.GetValueOrDefault(day, 0)
            });
        }

        var ratingGroups = await reviews
            .GroupBy(r => r.DishRating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ratingDict = ratingGroups.ToDictionary(x => x.Rating, x => x.Count);
        var ratingDistribution = Enumerable.Range(1, 10)
            .Select(i => new RatingDistributionItem
            {
                Rating = i,
                Count = ratingDict.GetValueOrDefault(i, 0)
            })
            .ToList();

        var categoryAverages = new CategoryAverages
        {
            Food = restaurant.AvgFoodScore ?? 0,
            Service = restaurant.AvgService ?? 0,
            Cleanliness = restaurant.AvgCleanliness ?? 0,
            Ambiance = restaurant.AvgAmbiance ?? 0
        };

        var topDishes = await _db.Dishes
            .AsNoTracking()
            .Where(d => d.RestaurantId == restaurant.RestaurantId && d.ReviewCount >= 3)
            .OrderByDescending(d => d.AvgRating)
            .Take(5)
            .Select(d => new DishRankingItem
            {
                DishName = d.DishName,
                AvgRating = d.AvgRating ?? 0,
                ReviewCount = d.ReviewCount
            })
            .ToListAsync(cancellationToken);

        return new BusinessChartDataDto
        {
            ReviewTrend = reviewTrend,
            RatingDistribution = ratingDistribution,
            CategoryAverages = categoryAverages,
            TopDishes = topDishes
        };
    }
}
