using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.SendHeartbeat;

public class SendHeartbeatHandler : IRequestHandler<SendHeartbeatCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SendHeartbeatHandler(ISmakoszDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(SendHeartbeatCommand request, CancellationToken cancellationToken)
    {
        var node = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeId == request.NodeId, cancellationToken);

        if (node is null)
        {
            node = new SystemNode
            {
                NodeId = request.NodeId,
                NodeType = NodeType.Gpu,
                Role = NodeRole.Worker
            };
            _db.SystemNodes.Add(node);
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
