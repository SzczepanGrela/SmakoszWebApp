using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Users.Dtos;

namespace Smakosz.Application.Features.Users.Queries.GetUserProfile;

public record GetUserProfileQuery(string Slug) : IRequest<ErrorOr<PublicUserProfileDto>>;
