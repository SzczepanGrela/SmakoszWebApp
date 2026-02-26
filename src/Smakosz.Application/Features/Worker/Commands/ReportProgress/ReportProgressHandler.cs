using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.ReportProgress;

public class ReportProgressHandler : IRequestHandler<ReportProgressCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;

    public ReportProgressHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<Success>> Handle(ReportProgressCommand request, CancellationToken cancellationToken)
    {
        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        if (job.Status != JobStatus.Processing)
            return Error.Conflict("JOB_NOT_PROCESSING", "Job is not in Processing state");

        _db.JobProgresses.Add(new JobProgress
        {
            JobId = job.JobId,
            Epoch = request.Epoch,
            Loss = request.Loss,
            Accuracy = request.Accuracy,
            LearningRate = request.LearningRate,
            CurrentStep = request.CurrentStep,
            TotalSteps = request.TotalSteps
        });

        if (request.CurrentStep.HasValue && request.TotalSteps.HasValue && request.TotalSteps.Value > 0)
            job.Progress = (int)(request.CurrentStep.Value * 100.0 / request.TotalSteps.Value);
        else if (request.Epoch.HasValue)
            job.Progress = request.Epoch.Value;

        job.ProgressMessage = request.Message;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
