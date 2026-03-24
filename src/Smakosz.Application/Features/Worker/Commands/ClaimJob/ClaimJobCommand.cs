using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Worker.Commands.ClaimJob;

public record ClaimJobCommand(
    int JobId,
    string WorkerNodeId
) : IRequest<ErrorOr<Success>>;
