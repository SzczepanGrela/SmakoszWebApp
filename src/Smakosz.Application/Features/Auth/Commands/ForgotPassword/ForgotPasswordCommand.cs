using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(
    string Email,
    string? TurnstileToken = null
) : IRequest<ErrorOr<Success>>;
