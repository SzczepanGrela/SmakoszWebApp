using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Home.Dtos;

namespace Smakosz.Application.Features.Home.Queries.GetHomeData;

public record GetHomeDataQuery : IRequest<ErrorOr<HomeDataDto>>;
