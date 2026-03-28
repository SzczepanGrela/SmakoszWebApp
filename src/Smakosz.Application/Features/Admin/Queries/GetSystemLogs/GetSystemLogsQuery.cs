using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using DomainLogLevel = Smakosz.Domain.Enums.LogLevel;

namespace Smakosz.Application.Features.Admin.Queries.GetSystemLogs;

public record GetSystemLogsQuery(PaginationParams Pagination, string? Level = null)
    : IRequest<ErrorOr<PagedResult<SystemLogDto>>>;

public class GetSystemLogsHandler : IRequestHandler<GetSystemLogsQuery, ErrorOr<PagedResult<SystemLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSystemLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<SystemLogDto>>> Handle(GetSystemLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.SystemLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Level) &&
            Enum.TryParse<DomainLogLevel>(request.Level, true, out var levelEnum))
        {
            query = query.Where(l => l.Level == levelEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new SystemLogDto
            {
                Id = l.Id,
                Source = l.Source,
                Level = l.Level.ToString(),
                Message = l.Message,
                Context = l.Context,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SystemLogDto>
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
