using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                n => n.PublicId == request.PublicId && n.UserId == _currentUser.UserId.Value,
                cancellationToken);

        if (notification is null)
            return DomainErrors.Notification.NotFound;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
