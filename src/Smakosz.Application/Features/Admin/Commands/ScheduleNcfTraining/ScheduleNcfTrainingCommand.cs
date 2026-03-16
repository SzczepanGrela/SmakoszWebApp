using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ScheduleNcfTraining;

public record ScheduleNcfTrainingCommand : IRequest<ErrorOr<Success>>;

public class ScheduleNcfTrainingHandler : IRequestHandler<ScheduleNcfTrainingCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INcfTrainingService _ncfTrainingService;

    public ScheduleNcfTrainingHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        INcfTrainingService ncfTrainingService)
    {
        _db = db;
        _currentUser = currentUser;
        _ncfTrainingService = ncfTrainingService;
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

        return await _ncfTrainingService.ScheduleAsync(cancellationToken);
    }
}
