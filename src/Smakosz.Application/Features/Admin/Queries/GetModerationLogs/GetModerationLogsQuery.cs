using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetModerationLogs;

public record GetModerationLogsQuery(PaginationParams Pagination, string? Actor = null, string? EntityType = null)
    : IRequest<ErrorOr<PagedResult<AdminModerationLogDto>>>;

public class GetModerationLogsHandler : IRequestHandler<GetModerationLogsQuery, ErrorOr<PagedResult<AdminModerationLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetModerationLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminModerationLogDto>>> Handle(GetModerationLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.ModerationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Actor) &&
            Enum.TryParse<ModerationActor>(request.Actor, true, out var actorEnum))
        {
            query = query.Where(l => l.Actor == actorEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType) &&
            Enum.TryParse<ModerationEntityType>(request.EntityType, true, out var entityTypeEnum))
        {
            query = query.Where(l => l.EntityType == entityTypeEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new AdminModerationLogDto
            {
                LogId = l.LogId,
                EntityType = l.EntityType.ToString(),
                EntityId = l.EntityId,
                Actor = l.Actor.ToString(),
                Verdict = l.Verdict.ToString(),
                ReasonCodes = l.ReasonCodes,
                AdminNote = l.AdminNote,
                ProcessedBy = l.ProcessedBy,
                ProcessedByUsername = l.ProcessedByUser != null ? l.ProcessedByUser.Username : null,
                AiScores = l.AiScores,
                CreatedAt = l.CreatedAt
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
