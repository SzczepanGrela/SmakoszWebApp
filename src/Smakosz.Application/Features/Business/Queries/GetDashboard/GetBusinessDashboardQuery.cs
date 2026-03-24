using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetDashboard;

public record GetBusinessDashboardQuery() : IRequest<ErrorOr<BusinessDashboardDto>>;

public class GetBusinessDashboardHandler : IRequestHandler<GetBusinessDashboardQuery, ErrorOr<BusinessDashboardDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessDashboardHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessDashboardDto>> Handle(GetBusinessDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var totalDishes = await _db.Dishes
            .CountAsync(d => d.RestaurantId == restaurant.RestaurantId, cancellationToken);

        var totalSections = await _db.MenuSections
            .CountAsync(ms => ms.RestaurantId == restaurant.RestaurantId, cancellationToken);

        var totalReviews = await _db.Reviews
            .CountAsync(r => r.RestaurantId == restaurant.RestaurantId, cancellationToken);

        return new BusinessDashboardDto
        {
            RestaurantName = restaurant.RestaurantName,
            ImageUrl = restaurant.ImageUrl,
            Status = restaurant.Status.ToString(),
            AvgRating = restaurant.AvgFoodScore,
            TotalReviews = totalReviews,
            TotalDishes = totalDishes,
            TotalMenuSections = totalSections
        };
    }
}
