using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetPendingPhotos;

public record GetPendingPhotosQuery(PaginationParams Pagination)
    : IRequest<ErrorOr<PagedResult<PhotoModerationDto>>>;

public class GetPendingPhotosHandler : IRequestHandler<GetPendingPhotosQuery, ErrorOr<PagedResult<PhotoModerationDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPendingPhotosHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<PhotoModerationDto>>> Handle(GetPendingPhotosQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var query = _db.MediaAssets
            .AsNoTracking()
            .Where(a => a.ModerationStatus == ContentModerationStatus.Pending
                      || a.ModerationStatus == ContentModerationStatus.NeedsReview);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(a => new PhotoModerationDto
            {
                AssetId = a.AssetId,
                PublicId = a.PublicId,
                Url = a.Url,
                EntityType = a.EntityType.ToString(),
                EntityId = a.EntityId,
                UploadedByUsername = a.Uploader != null ? a.Uploader.Username : null,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PhotoModerationDto>
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
