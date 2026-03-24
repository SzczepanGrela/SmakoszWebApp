using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMyProfile;

public record GetMyProfileQuery() : IRequest<ErrorOr<MyProfileDto>>;
