using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.UnfavoriteRestaurant;

public record UnfavoriteRestaurantCommand(string RestaurantSlug) : IRequest<ErrorOr<Success>>;

public class UnfavoriteRestaurantHandler : IRequestHandler<UnfavoriteRestaurantCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnfavoriteRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UnfavoriteRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Slug == request.RestaurantSlug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var favorite = await _db.FavoriteRestaurants
            .FirstOrDefaultAsync(
                f => f.UserId == _currentUser.UserId.Value && f.RestaurantId == restaurant.RestaurantId,
                cancellationToken);

        if (favorite is null)
            return DomainErrors.FavoriteRestaurant.NotFavorited;

        _db.FavoriteRestaurants.Remove(favorite);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
