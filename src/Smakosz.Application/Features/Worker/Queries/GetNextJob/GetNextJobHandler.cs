using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Worker.DTOs;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Queries.GetNextJob;

public class GetNextJobHandler : IRequestHandler<GetNextJobQuery, ErrorOr<WorkerJobDto?>>
{
    private readonly ISmakoszDbContext _db;

    public GetNextJobHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<WorkerJobDto?>> Handle(GetNextJobQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SystemJobs
            .Where(j => j.Status == JobStatus.Pending);

        if (!string.IsNullOrEmpty(request.Type))
            query = query.Where(j => j.Type == request.Type);

        var job = await query
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Select(j => new WorkerJobDto
            {
                JobId = j.JobId,
                Type = j.Type,
                Payload = j.Payload,
                EntityId = j.EntityId,
                EntityType = j.EntityType,
                MaxAttempts = j.MaxAttempts,
                Priority = j.Priority
            })
            .FirstOrDefaultAsync(cancellationToken);

        return job;
    }
}
