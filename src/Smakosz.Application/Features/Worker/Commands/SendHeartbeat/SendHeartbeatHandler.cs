using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Worker.Commands.SendHeartbeat;

public class SendHeartbeatHandler : IRequestHandler<SendHeartbeatCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SendHeartbeatHandler> _logger;

    public SendHeartbeatHandler(ISmakoszDbContext db, IDateTimeProvider clock, ILogger<SendHeartbeatHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SendHeartbeatCommand request, CancellationToken cancellationToken)
    {
        var node = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeId == request.NodeId, cancellationToken);

        if (node is null)
        {
            _logger.LogWarning("Heartbeat received for unknown node {NodeId}", request.NodeId);
            return DomainErrors.Node.NotFound;
        }

        node.Status = "online";
        node.LastHeartbeat = _clock.UtcNow;
        node.IpAddress = request.IpAddress;
        node.GpuName = request.GpuName;
        node.GpuMemoryTotal = request.GpuMemoryTotal;
        node.GpuMemoryUsed = request.GpuMemoryUsed;
        node.CurrentJobId = request.CurrentJobId;
        node.Metadata = request.Metadata;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
