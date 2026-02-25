using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetReports;

public record GetReportsQuery(PaginationParams Pagination, string? Status = null) : IRequest<ErrorOr<PagedResult<AdminReportDto>>>;

public class GetReportsHandler : IRequestHandler<GetReportsQuery, ErrorOr<PagedResult<AdminReportDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetReportsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminReportDto>>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.Reports.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<Smakosz.Domain.Enums.ReportStatus>(request.Status, true, out var statusEnum))
                query = query.Where(r => r.Status == statusEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new AdminReportDto
            {
                ReportId = r.ReportId,
                EntityType = r.EntityType.ToString(),
                EntityId = r.EntityId,
                Reason = r.Description ?? string.Empty,
                Status = r.Status.ToString(),
                ReporterUsername = r.Reporter.Username,
                CreatedAt = r.CreatedAt ?? DateTime.MinValue
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReportDto>
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
