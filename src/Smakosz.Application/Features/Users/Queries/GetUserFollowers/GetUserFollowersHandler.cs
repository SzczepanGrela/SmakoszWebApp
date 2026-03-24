using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Users.Dtos;

namespace Smakosz.Application.Features.Users.Queries.GetUserFollowers;

public class GetUserFollowersHandler : IRequestHandler<GetUserFollowersQuery, ErrorOr<PagedResult<UserListItemDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserFollowersHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<UserListItemDto>>> Handle(GetUserFollowersQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == request.Slug && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var currentUserId = _currentUser.UserId;

        var query = _db.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowedId == user.UserId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new UserListItemDto
            {
                PublicId = f.Follower.PublicId,
                Slug = f.Follower.Slug ?? string.Empty,
                Username = f.Follower.Username,
                AvatarUrl = f.Follower.AvatarUrl,
                ReviewCount = f.Follower.ReviewCount,
                IsFollowing = currentUserId.HasValue && _db.UserFollows.Any(uf => uf.FollowerId == currentUserId.Value && uf.FollowedId == f.FollowerId),
                FollowedAt = f.CreatedAt
            });

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemDto>
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
