using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestRestaurantClaim;

public record RequestRestaurantClaimCommand(
    Guid RestaurantPublicId,
    string Justification) : IRequest<ErrorOr<int>>;
