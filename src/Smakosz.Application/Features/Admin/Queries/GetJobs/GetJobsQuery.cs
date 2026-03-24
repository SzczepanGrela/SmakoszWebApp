using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetJobs;

public record GetJobsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<JobDto>>>;

public class GetJobsHandler : IRequestHandler<GetJobsQuery, ErrorOr<PagedResult<JobDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetJobsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<JobDto>>> Handle(GetJobsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.SystemJobs.AsNoTracking().AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(j => new JobDto
            {
                JobId = j.JobId,
                Type = j.Type,
                Status = j.Status.ToString(),
                Priority = j.Priority,
                Progress = j.Progress,
                CreatedAt = j.CreatedAt,
                FinishedAt = j.FinishedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<JobDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize)
            }
        };
    }
}
