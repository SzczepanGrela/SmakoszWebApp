using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.UpdateRestaurant;

public record UpdateRestaurantCommand(
    string? Name,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    int? CityId) : IRequest<ErrorOr<Success>>;

public class UpdateRestaurantHandler : IRequestHandler<UpdateRestaurantCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (request.Name is not null) restaurant.RestaurantName = request.Name;
        if (request.Description is not null) restaurant.Description = request.Description;
        if (request.Address is not null) restaurant.Address = request.Address;
        if (request.Phone is not null) restaurant.Phone = request.Phone;
        if (request.Email is not null) restaurant.Email = request.Email;
        if (request.Website is not null) restaurant.Website = request.Website;
        if (request.CityId.HasValue) restaurant.CityId = request.CityId.Value;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
