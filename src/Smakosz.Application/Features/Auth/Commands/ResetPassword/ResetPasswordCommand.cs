using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Code,
    string NewPassword
) : IRequest<ErrorOr<Success>>;
