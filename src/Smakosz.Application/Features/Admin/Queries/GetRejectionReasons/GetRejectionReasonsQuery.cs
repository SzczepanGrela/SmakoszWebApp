using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetRejectionReasons;

public record GetRejectionReasonsQuery(
    PaginationParams Pagination,
    string? Category = null,
    bool IncludeInactive = false)
    : IRequest<ErrorOr<PagedResult<AdminRejectionReasonDto>>>;

public class GetRejectionReasonsHandler
    : IRequestHandler<GetRejectionReasonsQuery, ErrorOr<PagedResult<AdminRejectionReasonDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRejectionReasonsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminRejectionReasonDto>>> Handle(
        GetRejectionReasonsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdminOrModerator)
            return DomainErrors.Admin.Forbidden;

        var query = _db.RejectionReasons.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!Enum.TryParse<RejectionReasonCategory>(request.Category, true, out var category))
                return DomainErrors.RejectionReason.InvalidCategory;

            query = query.Where(r => r.Category == category);
        }

        if (!request.IncludeInactive)
            query = query.Where(r => r.IsActive);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.AdminLabel)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new AdminRejectionReasonDto
            {
                ReasonCode = r.ReasonCode,
                Category = r.Category.ToString(),
                AdminLabel = r.AdminLabel,
                UserMessageTemplate = r.UserMessageTemplate,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRejectionReasonDto>
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
