using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteSystemNode;

public record DeleteSystemNodeCommand(string NodeId) : IRequest<ErrorOr<Success>>;

public class DeleteSystemNodeHandler : IRequestHandler<DeleteSystemNodeCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public DeleteSystemNodeHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteSystemNodeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var node = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeId == request.NodeId, cancellationToken);
        if (node is null)
            return DomainErrors.Node.NotFound;

        var thresholdRow = await _db.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "nodes.stale_threshold_days")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var thresholdDays = int.TryParse(thresholdRow, out var t) ? t : 7;

        var staleCutoff = _clock.UtcNow.AddDays(-thresholdDays);
        var isStale = node.LastHeartbeat is null || node.LastHeartbeat < staleCutoff;
        if (!isStale)
            return DomainErrors.Node.NotStale;

        _db.SystemNodes.Remove(node);

        _db.AuditLogs.Add(AuditLogHelper.BuildEntry(
            tableName: "system_nodes",
            recordId: 0,
            operation: AuditOperation.Delete,
            changedBy: _currentUser.UserId?.ToString(),
            oldSnapshot: new
            {
                node.NodeId,
                NodeType = node.NodeType.ToString(),
                node.Status,
                node.LastHeartbeat
            },
            newSnapshot: null));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
