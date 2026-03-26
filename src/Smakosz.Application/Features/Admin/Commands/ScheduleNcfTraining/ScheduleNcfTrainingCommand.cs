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

        var hasPendingOrProcessing = await _db.SystemJobs
            .AnyAsync(j => j.Type == "ncf_training"
                && (j.Status == JobStatus.Pending || j.Status == JobStatus.Processing),
                cancellationToken);

        if (hasPendingOrProcessing)
            return Error.Conflict("NCF_ALREADY_SCHEDULED", "NCF training is already pending or in progress");

        return await _ncfTrainingService.ScheduleAsync(cancellationToken);
    }
}
