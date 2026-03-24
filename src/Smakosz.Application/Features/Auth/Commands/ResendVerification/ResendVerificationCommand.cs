using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.ResendVerification;

public record ResendVerificationCommand(string Email) : IRequest<ErrorOr<Success>>;
