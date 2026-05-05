using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetSystemNodes;

public record GetSystemNodesQuery() : IRequest<ErrorOr<SystemNodesResponseDto>>;

public class GetSystemNodesHandler : IRequestHandler<GetSystemNodesQuery, ErrorOr<SystemNodesResponseDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSystemNodesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<SystemNodesResponseDto>> Handle(GetSystemNodesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var items = await _db.SystemNodes.AsNoTracking()
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.NodeId)
            .Select(n => new SystemNodeDto
            {
                NodeId = n.NodeId,
                IpAddress = n.IpAddress,
                Status = n.Status,
                NodeType = n.NodeType.ToString(),
                Role = n.Role != null ? n.Role.ToString() : null,
                Hostname = n.Hostname,
                GpuName = n.GpuName,
                GpuMemoryTotal = n.GpuMemoryTotal,
                GpuMemoryUsed = n.GpuMemoryUsed,
                CurrentJobId = n.CurrentJobId,
                LastHeartbeat = n.LastHeartbeat
            })
            .ToListAsync(cancellationToken);

        var thresholdRow = await _db.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "nodes.stale_threshold_days")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var thresholdDays = int.TryParse(thresholdRow, out var t) ? t : 7;

        return new SystemNodesResponseDto { Nodes = items, StaleThresholdDays = thresholdDays };
    }
}
