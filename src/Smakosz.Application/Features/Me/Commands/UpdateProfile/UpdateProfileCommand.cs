using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public record UpdateProfileCommand(string? Username, string? Bio, string? AvatarUrl) : IRequest<ErrorOr<Success>>;
