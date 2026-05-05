using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.AddSystemNode;

public record AddSystemNodeCommand(
    string NodeId,
    string NodeType,
    string? MacAddress,
    string? WolGatewayId
) : IRequest<ErrorOr<Success>>;

public class AddSystemNodeHandler : IRequestHandler<AddSystemNodeCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AddSystemNodeHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AddSystemNodeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (!Enum.TryParse<NodeType>(request.NodeType, ignoreCase: true, out var nodeType))
            return Error.Validation("NODE_INVALID_TYPE", $"Nieznany NodeType: {request.NodeType}");

        var exists = await _db.SystemNodes.AnyAsync(n => n.NodeId == request.NodeId, cancellationToken);
        if (exists)
            return DomainErrors.Node.AlreadyExists;

        if (nodeType == NodeType.Gpu)
        {
            if (string.IsNullOrWhiteSpace(request.MacAddress) || string.IsNullOrWhiteSpace(request.WolGatewayId))
                return Error.Validation("NODE_GPU_REQUIRES_MAC_AND_GATEWAY",
                    "GPU node wymaga MacAddress i WolGatewayId");

            var gatewayOk = await _db.SystemNodes.AnyAsync(
                n => n.NodeId == request.WolGatewayId
                     && (n.NodeType == NodeType.Orchestrator || n.NodeType == NodeType.RbpiGateway),
                cancellationToken);
            if (!gatewayOk)
                return DomainErrors.Node.InvalidGatewayReference;
        }

        _db.SystemNodes.Add(new SystemNode
        {
            NodeId = request.NodeId,
            NodeType = nodeType,
            MacAddress = request.MacAddress,
            WolGatewayId = request.WolGatewayId,
            Status = "unknown",
            LastHeartbeat = null
        });

        _db.AuditLogs.Add(AuditLogHelper.BuildEntry(
            tableName: "system_nodes",
            recordId: 0,
            operation: AuditOperation.Insert,
            changedBy: _currentUser.UserId?.ToString(),
            oldSnapshot: null,
            newSnapshot: new { request.NodeId, request.NodeType, request.MacAddress, request.WolGatewayId }));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
