using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.Resend2fa;

public record Resend2faCommand(string Email) : IRequest<ErrorOr<Success>>;
