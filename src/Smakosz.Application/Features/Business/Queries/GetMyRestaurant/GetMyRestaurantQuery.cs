using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetMyRestaurant;

public record GetMyRestaurantQuery() : IRequest<ErrorOr<BusinessRestaurantDto>>;

public class GetMyRestaurantHandler : IRequestHandler<GetMyRestaurantQuery, ErrorOr<BusinessRestaurantDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessRestaurantDto>> Handle(GetMyRestaurantQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        return new BusinessRestaurantDto
        {
            RestaurantId = restaurant.RestaurantId,
            Name = restaurant.RestaurantName,
            Slug = restaurant.Slug ?? string.Empty,
            Description = restaurant.Description,
            Address = restaurant.Address,
            Phone = restaurant.Phone,
            Email = restaurant.Email,
            Website = restaurant.Website,
            ImageUrl = restaurant.ImageUrl,
            CityId = restaurant.CityId,
            Status = restaurant.Status.ToString()
        };
    }
}
