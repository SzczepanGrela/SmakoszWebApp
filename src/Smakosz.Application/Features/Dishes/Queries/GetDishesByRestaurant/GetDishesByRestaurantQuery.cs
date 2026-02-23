using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Dtos;

namespace Smakosz.Application.Features.Dishes.Queries.GetDishesByRestaurant;

public record GetDishesByRestaurantQuery(
    string RestaurantSlug,
    PaginationParams Pagination
) : IRequest<ErrorOr<PagedResult<DishCardDto>>>;
