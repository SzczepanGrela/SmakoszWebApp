using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ScheduleNcfTraining;

public record ScheduleNcfTrainingCommand : IRequest<ErrorOr<Success>>;

public class ScheduleNcfTrainingHandler : IRequestHandler<ScheduleNcfTrainingCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INcfTrainingService _ncfTrainingService;
    private readonly IDateTimeProvider _clock;

    public ScheduleNcfTrainingHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        INcfTrainingService ncfTrainingService,
        IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _ncfTrainingService = ncfTrainingService;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(ScheduleNcfTrainingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var blockingJob = await _db.SystemJobs
            .Where(j => j.Type == "ncf_training"
                && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing))
            .Select(j => new { j.JobId, j.Status, j.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (blockingJob is not null)
            return Error.Conflict("NCF_ALREADY_SCHEDULED",
                $"Trening NCF jest już {(blockingJob.Status == JobStatus.Pending ? "oczekujący" : "w trakcie")} " +
                $"(Job #{blockingJob.JobId}, utworzony {blockingJob.CreatedAt:dd.MM HH:mm}). " +
                $"Anuluj go najpierw.");

        _db.SystemJobs.Add(new SystemJob
        {
            Type = "ncf_training",
            Status = JobStatus.Pending,
            Priority = 10,
            Payload = null,
            Progress = 0,
            Attempts = 0,
            MaxAttempts = 3,
            CreatedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return await _ncfTrainingService.ScheduleAsync(cancellationToken);
    }
}
