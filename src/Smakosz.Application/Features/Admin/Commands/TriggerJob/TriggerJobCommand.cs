using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.TriggerJob;

public record TriggerJobCommand(int JobId) : IRequest<ErrorOr<Success>>;

public class TriggerJobHandler : IRequestHandler<TriggerJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TriggerJobHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(TriggerJobCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        job.Status = JobStatus.Pending;
        job.Attempts = 0;
        job.Progress = 0;
        job.ErrorMessage = null;
        job.ErrorLog = null;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
