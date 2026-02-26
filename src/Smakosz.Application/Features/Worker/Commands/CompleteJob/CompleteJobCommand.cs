using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Worker.Commands.CompleteJob;

public record CompleteJobCommand(
    int JobId,
    string Result,
    int ProcessingTimeMs
) : IRequest<ErrorOr<Success>>;
