using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Restaurants.Dtos;

namespace Smakosz.Application.Features.Restaurants.Queries.GetRestaurants;

public record GetRestaurantsQuery(
    PaginationParams Pagination,
    int? CityId = null,
    int? CuisineTypeId = null,
    int? MinPrice = null,
    int? MaxPrice = null,
    string SortBy = "trending"
) : IRequest<ErrorOr<PagedResult<RestaurantCardDto>>>;
