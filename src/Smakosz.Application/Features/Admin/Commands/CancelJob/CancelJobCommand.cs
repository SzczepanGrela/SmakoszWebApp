using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CancelJob;

public record CancelJobCommand(int JobId) : IRequest<ErrorOr<Success>>;

public class CancelJobHandler : IRequestHandler<CancelJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public CancelJobHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(CancelJobCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
            return Error.Conflict("JOB_CANNOT_CANCEL", "Cannot cancel a completed or already cancelled job");

        if (!string.IsNullOrEmpty(job.WorkerNode))
        {
            var node = await _db.SystemNodes
                .FirstOrDefaultAsync(n => n.NodeId == job.WorkerNode, cancellationToken);
            if (node is not null)
                node.CurrentJobId = null;
        }

        job.Status = JobStatus.Cancelled;
        job.WorkerNode = null;
        job.FinishedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
