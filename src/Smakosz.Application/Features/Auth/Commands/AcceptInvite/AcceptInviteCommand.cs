using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.AcceptInvite;

public record AcceptInviteCommand(
    string Email,
    string Code,
    string NewPassword
) : IRequest<ErrorOr<Success>>;
