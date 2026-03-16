using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Users.Dtos;

namespace Smakosz.Application.Features.Users.Queries.GetUserProfile;

public class GetUserProfileHandler : IRequestHandler<GetUserProfileQuery, ErrorOr<PublicUserProfileDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserProfileHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PublicUserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == request.Slug && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var isFollowed = _currentUser.UserId.HasValue &&
            await _db.UserFollows.AnyAsync(
                f => f.FollowerId == _currentUser.UserId.Value && f.FollowedId == user.UserId,
                cancellationToken);

        return new PublicUserProfileDto
        {
            PublicId = user.PublicId,
            Slug = user.Slug ?? string.Empty,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            ReviewCount = user.ReviewCount,
            FollowersCount = user.FollowersCount,
            FollowingCount = user.FollowingCount,
            CreatedAt = user.CreatedAt,
            IsFollowedByCurrentUser = isFollowed
        };
    }
}
