using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Queries.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery() : IRequest<ErrorOr<int>>;

public class GetUnreadNotificationCountHandler : IRequestHandler<GetUnreadNotificationCountQuery, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadNotificationCountHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var count = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        return count;
    }
}
