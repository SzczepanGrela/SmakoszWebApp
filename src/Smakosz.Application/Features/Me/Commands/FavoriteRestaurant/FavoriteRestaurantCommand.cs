using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Me.Commands.FavoriteRestaurant;

public record FavoriteRestaurantCommand(string RestaurantSlug) : IRequest<ErrorOr<Success>>;

public class FavoriteRestaurantHandler : IRequestHandler<FavoriteRestaurantCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FavoriteRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(FavoriteRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Slug == request.RestaurantSlug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var alreadyFavorited = await _db.FavoriteRestaurants.AnyAsync(
            f => f.UserId == _currentUser.UserId.Value && f.RestaurantId == restaurant.RestaurantId,
            cancellationToken);

        if (alreadyFavorited)
            return DomainErrors.FavoriteRestaurant.AlreadyFavorited;

        _db.FavoriteRestaurants.Add(new Domain.Entities.FavoriteRestaurant
        {
            UserId = _currentUser.UserId.Value,
            RestaurantId = restaurant.RestaurantId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
