using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.ClaimJob;

public class ClaimJobHandler : IRequestHandler<ClaimJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ClaimJobHandler(ISmakoszDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(ClaimJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        if (job.Status != JobStatus.Pending)
            return Error.Conflict("JOB_ALREADY_CLAIMED", "Job is not in Pending state");

        job.Status = JobStatus.Processing;
        job.WorkerNode = request.WorkerNodeId;
        job.StartedAt = _clock.UtcNow;
        job.Attempts++;

        var node = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeId == request.WorkerNodeId, cancellationToken);

        if (node is not null)
            node.CurrentJobId = job.JobId;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("JOB_ALREADY_CLAIMED", "Job was claimed by another worker");
        }

        return Result.Success;
    }
}
