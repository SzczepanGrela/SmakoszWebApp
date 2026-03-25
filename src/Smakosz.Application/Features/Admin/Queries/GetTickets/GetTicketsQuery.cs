using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetTickets;

public record GetTicketsQuery(PaginationParams Pagination, string? Status = null, string? TicketType = null)
    : IRequest<ErrorOr<PagedResult<AdminTicketDto>>>;

public class GetTicketsHandler : IRequestHandler<GetTicketsQuery, ErrorOr<PagedResult<AdminTicketDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTicketsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminTicketDto>>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.SystemTickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<TicketStatus>(request.Status, true, out var statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.TicketType) &&
            Enum.TryParse<TicketType>(request.TicketType, true, out var typeEnum))
        {
            query = query.Where(t => t.TicketType == typeEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(t => t.AssignedAdmin)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
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
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize)
            }
        };
    }
}
