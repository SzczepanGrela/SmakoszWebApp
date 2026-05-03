using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetSystemNodes;

public record GetSystemNodesQuery() : IRequest<ErrorOr<List<SystemNodeDto>>>;

public class GetSystemNodesHandler : IRequestHandler<GetSystemNodesQuery, ErrorOr<List<SystemNodeDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSystemNodesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<SystemNodeDto>>> Handle(GetSystemNodesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var items = await _db.SystemNodes.AsNoTracking()
            .Where(n => n.NodeType == NodeType.Gpu || n.NodeType == NodeType.RpiGateway)
            .OrderBy(n => n.NodeType)
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

        return items;
    }
}
