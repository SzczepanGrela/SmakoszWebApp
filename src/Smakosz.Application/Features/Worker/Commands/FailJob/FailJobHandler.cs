using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.FailJob;

public class FailJobHandler : IRequestHandler<FailJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;

    public FailJobHandler(ISmakoszDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(FailJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        if (job.Status != JobStatus.Processing)
            return Error.Conflict("JOB_NOT_PROCESSING", "Job is not in Processing state");

        job.ErrorMessage = request.ErrorMessage;
        job.ErrorLog = request.ErrorLog;

        if (!string.IsNullOrEmpty(job.WorkerNode))
        {
            var node = await _db.SystemNodes
                .FirstOrDefaultAsync(n => n.NodeId == job.WorkerNode, cancellationToken);
            if (node is not null)
                node.CurrentJobId = null;
        }

        if (request.Retryable && job.Attempts < job.MaxAttempts)
        {
            job.Status = JobStatus.Pending;
            job.WorkerNode = null;
            job.StartedAt = null;
            job.Progress = 0;
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.FinishedAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
