using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Dishes.Dtos;

namespace Smakosz.Application.Features.Dishes.Queries.GetRandomDish;

public record GetRandomDishQuery : IRequest<ErrorOr<DishCardDto>>;
