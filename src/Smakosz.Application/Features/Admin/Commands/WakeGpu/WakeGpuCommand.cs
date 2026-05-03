using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.WakeGpu;

public record WakeGpuCommand : IRequest<ErrorOr<GpuWakeResult>>;

public class WakeGpuHandler : IRequestHandler<WakeGpuCommand, ErrorOr<GpuWakeResult>>
{
    private readonly IGpuWakeService _gpuWake;
    private readonly ICurrentUserService _currentUser;
    private readonly ISmakoszDbContext _db;

    public WakeGpuHandler(
        IGpuWakeService gpuWake,
        ICurrentUserService currentUser,
        ISmakoszDbContext db)
    {
        _gpuWake = gpuWake;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<ErrorOr<GpuWakeResult>> Handle(WakeGpuCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var result = await _gpuWake.WakeAsync(cancellationToken);

        if (result.Status == GpuWakeStatus.Sent)
        {
            _db.AuditLogs.Add(AuditLogHelper.BuildEntry(
                tableName: "system_nodes",
                recordId: 0,
                operation: AuditOperation.Update,
                changedBy: _currentUser.UserId?.ToString(),
                oldSnapshot: null,
                newSnapshot: new { triggerSource = "manual_admin_panel", nodeType = "Gpu" }));
            await _db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
