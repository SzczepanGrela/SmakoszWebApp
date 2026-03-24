using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Worker.DTOs;

namespace Smakosz.Application.Features.Worker.Queries.GetWorkerConfig;

public record GetWorkerConfigQuery : IRequest<ErrorOr<WorkerConfigDto>>;
