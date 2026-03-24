using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserDetail;

public record GetUserDetailQuery(Guid PublicId) : IRequest<ErrorOr<AdminUserDetailDto>>;

public class GetUserDetailHandler : IRequestHandler<GetUserDetailQuery, ErrorOr<AdminUserDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserDetailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<AdminUserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.PublicId == request.PublicId && !u.IsDeleted)
            .Select(u => new AdminUserDetailDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role.ToString(),
                Status = u.IsBanned ? "Banned" : u.IsActive ? "Active" : "Inactive",
                EmailVerified = u.EmailVerified,
                IsBanned = u.IsBanned,
                Is2faEnabled = u.Is2faEnabled,
                AvatarUrl = u.AvatarUrl,
                Slug = u.Slug,
                ReviewCount = u.ReviewCount,
                FollowersCount = u.FollowersCount,
                FollowingCount = u.FollowingCount,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        return user;
    }
}
