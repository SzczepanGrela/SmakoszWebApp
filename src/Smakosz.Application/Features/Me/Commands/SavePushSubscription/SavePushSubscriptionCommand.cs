using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Me.Commands.SavePushSubscription;

public record SavePushSubscriptionCommand(string Endpoint, string P256dh, string Auth, string? DeviceName = null) : IRequest<ErrorOr<Success>>;

public class SavePushSubscriptionHandler : IRequestHandler<SavePushSubscriptionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SavePushSubscriptionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(SavePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.DeviceName = request.DeviceName;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                DeviceName = request.DeviceName,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
