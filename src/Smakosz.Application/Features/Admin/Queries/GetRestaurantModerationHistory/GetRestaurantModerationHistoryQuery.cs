using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetRestaurantModerationHistory;

public record GetRestaurantModerationHistoryQuery(int RestaurantId, PaginationParams Pagination)
    : IRequest<ErrorOr<PagedResult<AdminModerationLogDto>>>;

public class GetRestaurantModerationHistoryHandler
    : IRequestHandler<GetRestaurantModerationHistoryQuery, ErrorOr<PagedResult<AdminModerationLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRestaurantModerationHistoryHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminModerationLogDto>>> Handle(
        GetRestaurantModerationHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.ModerationLogs
            .AsNoTracking()
            .Where(m => m.EntityType == ModerationEntityType.Restaurant && m.EntityId == request.RestaurantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(m => new AdminModerationLogDto
            {
                LogId = m.LogId,
                EntityType = m.EntityType.ToString(),
                EntityId = m.EntityId,
                Actor = m.Actor.ToString(),
                Verdict = m.Verdict.ToString(),
                AdminNote = m.AdminNote,
                ProcessedByUsername = m.ProcessedByUser != null ? m.ProcessedByUser.Username : null,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminModerationLogDto>
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
