using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Restaurants.Dtos;

namespace Smakosz.Application.Features.Restaurants.Queries.GetRestaurantBySlug;

public record GetRestaurantBySlugQuery(string Slug) : IRequest<ErrorOr<RestaurantDetailDto>>;
