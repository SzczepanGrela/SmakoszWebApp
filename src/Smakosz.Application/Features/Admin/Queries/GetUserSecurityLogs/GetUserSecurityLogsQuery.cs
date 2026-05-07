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
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidationConfigProvider _config;

    public GetUserSecurityLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IValidationConfigProvider config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ErrorOr<PagedResult<SecurityLogDto>>> Handle(GetUserSecurityLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var pageSize = _config.GetInt("admin.list_page_size", 10);

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
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SecurityLogDto
            {
                LogId = s.LogId,
                EventType = s.EventType.ToString(),
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
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };
    }
}
