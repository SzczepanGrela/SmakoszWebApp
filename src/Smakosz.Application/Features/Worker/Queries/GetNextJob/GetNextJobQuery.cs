using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Worker.DTOs;

namespace Smakosz.Application.Features.Worker.Queries.GetNextJob;

public record GetNextJobQuery(
    string? Type,
    string WorkerNodeId
) : IRequest<ErrorOr<WorkerJobDto>>;
