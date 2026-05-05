using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserAdminFollowers;

public record GetUserAdminFollowersQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<AdminUserFollowerDto>>>;

public class GetUserAdminFollowersHandler : IRequestHandler<GetUserAdminFollowersQuery, ErrorOr<PagedResult<AdminUserFollowerDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserAdminFollowersHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminUserFollowerDto>>> Handle(GetUserAdminFollowersQuery request, CancellationToken cancellationToken)
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

        var query = _db.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowedId == user.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((request.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(f => new AdminUserFollowerDto
            {
                PublicId = f.Follower.PublicId,
                Username = f.Follower.Username,
                AvatarUrl = f.Follower.AvatarUrl,
                FollowedAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserFollowerDto>
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
