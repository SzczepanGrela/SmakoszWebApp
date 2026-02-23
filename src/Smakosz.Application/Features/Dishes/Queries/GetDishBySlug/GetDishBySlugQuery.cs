using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Dishes.Dtos;

namespace Smakosz.Application.Features.Dishes.Queries.GetDishBySlug;

public record GetDishBySlugQuery(string Slug) : IRequest<ErrorOr<DishDetailDto>>;
