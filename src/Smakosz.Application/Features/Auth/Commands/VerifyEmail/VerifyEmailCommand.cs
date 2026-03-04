using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand(string Email, string Code) : IRequest<ErrorOr<Success>>;
