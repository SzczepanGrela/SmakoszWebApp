using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Worker.Commands.SendHeartbeat;

public record SendHeartbeatCommand(
    string NodeId,
    string? IpAddress,
    string? GpuName,
    int? GpuMemoryTotal,
    int? GpuMemoryUsed,
    int? CurrentJobId,
    string? Metadata
) : IRequest<ErrorOr<Success>>;
