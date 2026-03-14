using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using DomainSecurityEventType = Smakosz.Domain.Enums.SecurityEventType;

namespace Smakosz.Application.Features.Admin.Queries.GetSecurityLogs;

public record GetSecurityLogsQuery(PaginationParams Pagination, string? EventType = null)
    : IRequest<ErrorOr<PagedResult<SecurityLogDto>>>;

public class GetSecurityLogsHandler : IRequestHandler<GetSecurityLogsQuery, ErrorOr<PagedResult<SecurityLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSecurityLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<SecurityLogDto>>> Handle(GetSecurityLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.SecurityLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EventType) &&
            Enum.TryParse<DomainSecurityEventType>(request.EventType, true, out var eventTypeEnum))
        {
            query = query.Where(l => l.EventType == eventTypeEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new SecurityLogDto
            {
                LogId = l.LogId,
                EventType = l.EventType != null ? l.EventType.ToString() : null,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Email = l.Email,
                UserId = l.UserId,
                Details = l.Details,
                CountryCode = l.CountryCode,
                City = l.City,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SecurityLogDto>
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
