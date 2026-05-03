using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestNewRestaurant;

public record RequestNewRestaurantCommand(
    string Name,
    string Address,
    string? Phone,
    string? Email,
    string? Description,
    int? CityId,
    int? CuisineTypeId) : IRequest<ErrorOr<int>>;
