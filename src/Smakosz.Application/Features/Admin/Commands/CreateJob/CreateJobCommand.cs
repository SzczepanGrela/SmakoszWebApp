using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateJob;

public record CreateJobCommand(
    string Type,
    int Priority,
    string? Payload,
    string? EntityId,
    string? EntityType) : IRequest<ErrorOr<int>>;

public class CreateJobHandler : IRequestHandler<CreateJobCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public CreateJobHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ErrorOr<int>> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var job = new SystemJob
        {
            Type = request.Type,
            Status = JobStatus.Pending,
            Priority = request.Priority,
            Payload = request.Payload,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            CreatedAt = _clock.UtcNow,
            MaxAttempts = 3
        };

        _db.SystemJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return job.JobId;
    }
}
