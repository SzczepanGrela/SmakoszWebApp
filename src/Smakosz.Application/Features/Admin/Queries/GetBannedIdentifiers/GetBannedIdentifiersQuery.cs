using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetBannedIdentifiers;

public record GetBannedIdentifiersQuery(PaginationParams Pagination, string? Type = null, bool IncludeExpired = false)
    : IRequest<ErrorOr<PagedResult<AdminBannedIdentifierDto>>>;

public class GetBannedIdentifiersHandler : IRequestHandler<GetBannedIdentifiersQuery, ErrorOr<PagedResult<AdminBannedIdentifierDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBannedIdentifiersHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminBannedIdentifierDto>>> Handle(GetBannedIdentifiersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.BannedIdentifiers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<BannedIdentifierType>(request.Type, true, out var parsedType))
        {
            query = query.Where(b => b.Type == parsedType);
        }

        var now = DateTime.UtcNow;
        if (!request.IncludeExpired)
        {
            query = query.Where(b => b.ExpiresAt == null || b.ExpiresAt > now);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.BannedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(b => new AdminBannedIdentifierDto
            {
                BanId = b.BanId,
                Type = b.Type.ToString(),
                Value = b.Value,
                Reason = b.Reason,
                BannedByUsername = b.BannedByUser != null ? b.BannedByUser.Username : null,
                BannedAt = b.BannedAt,
                ExpiresAt = b.ExpiresAt,
                IsExpired = b.ExpiresAt != null && b.ExpiresAt <= now
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminBannedIdentifierDto>
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
