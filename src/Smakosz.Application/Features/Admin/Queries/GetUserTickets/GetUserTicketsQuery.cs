using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserTickets;

public record GetUserTicketsQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<AdminTicketDto>>>;

public class GetUserTicketsHandler : IRequestHandler<GetUserTicketsQuery, ErrorOr<PagedResult<AdminTicketDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserTicketsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminTicketDto>>> Handle(GetUserTicketsQuery request, CancellationToken cancellationToken)
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

        var query = _db.SystemTickets
            .AsNoTracking()
            .Where(t => t.RequesterId == user.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(t => new AdminTicketDto
            {
                TicketId = t.TicketId,
                TicketType = t.TicketType.ToString(),
                ReferenceId = t.ReferenceId,
                Status = t.Status.ToString(),
                Priority = t.Priority,
                Description = t.Description,
                AssignedAdminUsername = t.AssignedAdmin != null ? t.AssignedAdmin.Username : null,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminTicketDto>
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
