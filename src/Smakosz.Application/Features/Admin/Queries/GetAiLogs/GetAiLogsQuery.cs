using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetAiLogs;

public record GetAiLogsQuery(PaginationParams Pagination, string? ModelType = null, bool? Fallback = null)
    : IRequest<ErrorOr<PagedResult<AiLogDto>>>;

public class GetAiLogsHandler : IRequestHandler<GetAiLogsQuery, ErrorOr<PagedResult<AiLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAiLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AiLogDto>>> Handle(GetAiLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.AiLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ModelType))
        {
            query = query.Where(l => l.ModelType == request.ModelType);
        }

        if (request.Fallback.HasValue)
        {
            query = query.Where(l => l.Fallback == request.Fallback.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new AiLogDto
            {
                LogId = l.LogId,
                ModelType = l.ModelType,
                ModelName = l.ModelName,
                ModelVersion = l.ModelVersion,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                InputSummary = l.InputSummary,
                Scores = l.Scores,
                Verdict = l.Verdict,
                ProcessingTimeMs = l.ProcessingTimeMs,
                Fallback = l.Fallback,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AiLogDto>
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
