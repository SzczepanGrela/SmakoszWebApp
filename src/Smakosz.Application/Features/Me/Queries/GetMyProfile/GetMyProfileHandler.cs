using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMyProfile;

public class GetMyProfileHandler : IRequestHandler<GetMyProfileQuery, ErrorOr<MyProfileDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<MyProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var followersCount = await _db.UserFollows
            .CountAsync(f => f.FollowedId == user.UserId, cancellationToken);

        var followingCount = await _db.UserFollows
            .CountAsync(f => f.FollowerId == user.UserId, cancellationToken);

        return new MyProfileDto
        {
            PublicId = user.PublicId,
            Slug = user.Slug ?? string.Empty,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            EmailVerified = user.EmailVerified,
            Is2faEnabled = user.Is2faEnabled,
            ReviewCount = user.ReviewCount,
            FollowersCount = followersCount,
            FollowingCount = followingCount,
            CreatedAt = user.CreatedAt
        };
    }
}
