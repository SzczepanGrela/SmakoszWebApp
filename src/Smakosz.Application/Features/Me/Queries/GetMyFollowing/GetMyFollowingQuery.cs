using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMyFollowing;

public record GetMyFollowingQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<FollowUserDto>>>;

public class GetMyFollowingHandler : IRequestHandler<GetMyFollowingQuery, ErrorOr<PagedResult<FollowUserDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyFollowingHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<FollowUserDto>>> Handle(GetMyFollowingQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var query = _db.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowerId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(f => new FollowUserDto
            {
                UserId = f.Followed.UserId,
                Username = f.Followed.Username,
                Slug = f.Followed.Slug,
                AvatarUrl = f.Followed.AvatarUrl,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FollowUserDto>
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
