using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetRegistrationStatus;

public record GetRegistrationStatusQuery() : IRequest<ErrorOr<RegistrationStatusDto>>;

public class GetRegistrationStatusHandler : IRequestHandler<GetRegistrationStatusQuery, ErrorOr<RegistrationStatusDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRegistrationStatusHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<RegistrationStatusDto>> Handle(
        GetRegistrationStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
        {
            return new RegistrationStatusDto
            {
                HasRestaurant = false,
                Status = null,
                RestaurantName = null,
                RestaurantSlug = null
            };
        }

        return new RegistrationStatusDto
        {
            HasRestaurant = true,
            Status = restaurant.Status.ToString(),
            RestaurantName = restaurant.RestaurantName,
            RestaurantSlug = restaurant.Slug
        };
    }
}
