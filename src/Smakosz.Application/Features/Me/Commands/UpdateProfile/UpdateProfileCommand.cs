using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public record UpdateProfileCommand(string? Username, string? Bio) : IRequest<ErrorOr<Success>>;
