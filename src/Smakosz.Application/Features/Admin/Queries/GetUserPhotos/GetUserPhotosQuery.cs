using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserPhotos;

public record GetUserPhotosQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<PhotoModerationDto>>>;

public class GetUserPhotosHandler : IRequestHandler<GetUserPhotosQuery, ErrorOr<PagedResult<PhotoModerationDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserPhotosHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<PhotoModerationDto>>> Handle(GetUserPhotosQuery request, CancellationToken cancellationToken)
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

        var query = _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.UploadedBy == user.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(m => new PhotoModerationDto
            {
                AssetId = m.AssetId,
                PublicId = m.PublicId,
                Url = m.Url,
                EntityType = m.EntityType.ToString(),
                EntityId = m.EntityId,
                UploadedByUsername = m.Uploader != null ? m.Uploader.Username : null,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PhotoModerationDto>
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
