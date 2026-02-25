using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessStats;

public record GetBusinessStatsQuery() : IRequest<ErrorOr<BusinessStatsDto>>;

public class GetBusinessStatsHandler : IRequestHandler<GetBusinessStatsQuery, ErrorOr<BusinessStatsDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessStatsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessStatsDto>> Handle(GetBusinessStatsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var reviews = _db.Reviews
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurant.RestaurantId && !r.IsDeleted);

        var totalReviews = await reviews.CountAsync(cancellationToken);

        var reviewsThisMonth = await reviews
            .CountAsync(r => r.CreatedAt >= thisMonthStart, cancellationToken);

        var reviewsLastMonth = await reviews
            .CountAsync(r => r.CreatedAt >= lastMonthStart && r.CreatedAt < thisMonthStart, cancellationToken);

        var averageRating = restaurant.AvgFoodScore;

        return new BusinessStatsDto
        {
            TotalReviews = totalReviews,
            AverageRating = averageRating,
            ReviewsThisMonth = reviewsThisMonth,
            ReviewsLastMonth = reviewsLastMonth
        };
    }
}
