using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetAuditLogs;

public record GetAuditLogsQuery(PaginationParams Pagination, string? TableName = null)
    : IRequest<ErrorOr<PagedResult<AuditLogDto>>>;

public class GetAuditLogsHandler : IRequestHandler<GetAuditLogsQuery, ErrorOr<PagedResult<AuditLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAuditLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
            query = query.Where(l => l.TableName == request.TableName);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.ChangedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new AuditLogDto
            {
                AuditLogId = l.AuditLogId,
                TableName = l.TableName,
                RecordId = l.RecordId,
                Operation = l.Operation.ToString(),
                ChangedBy = l.ChangedBy,
                ChangedAt = l.ChangedAt,
                OldValues = l.OldValues,
                NewValues = l.NewValues
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>
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
