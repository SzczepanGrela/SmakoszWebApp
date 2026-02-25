using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand() : IRequest<ErrorOr<Success>>;

public class MarkAllNotificationsReadHandler : IRequestHandler<MarkAllNotificationsReadCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var unread = await _db.Notifications
            .Where(n => n.UserId == _currentUser.UserId.Value && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
            n.IsRead = true;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
