using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Worker.Commands.FailJob;

public record FailJobCommand(
    int JobId,
    string ErrorMessage,
    string? ErrorLog,
    bool Retryable
) : IRequest<ErrorOr<Success>>;
