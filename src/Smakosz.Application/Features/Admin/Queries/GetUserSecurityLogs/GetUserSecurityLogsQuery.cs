using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserSecurityLogs;

public record GetUserSecurityLogsQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<SecurityLogDto>>>;

public class GetUserSecurityLogsHandler : IRequestHandler<GetUserSecurityLogsQuery, ErrorOr<PagedResult<SecurityLogDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserSecurityLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<SecurityLogDto>>> Handle(GetUserSecurityLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.PublicId == request.PublicId && !u.IsDeleted)
            .Select(u => new { u.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var query = _db.SecurityLogs
            .AsNoTracking()
            .Where(s => s.UserId == user.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new SecurityLogDto
            {
                LogId = s.LogId,
                EventType = s.EventType != null ? s.EventType.ToString() : null,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                Email = s.Email,
                UserId = s.UserId,
                Details = s.Details,
                CountryCode = s.CountryCode,
                City = s.City,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SecurityLogDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Page,
                PageSize = PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize)
            }
        };
    }
}
