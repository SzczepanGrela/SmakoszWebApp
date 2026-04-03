using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.RemovePushSubscription;

public record RemovePushSubscriptionCommand(string Endpoint) : IRequest<ErrorOr<Success>>;

public class RemovePushSubscriptionHandler : IRequestHandler<RemovePushSubscriptionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RemovePushSubscriptionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(RemovePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == _currentUser.UserId.Value, cancellationToken);

        if (subscription is not null)
            _db.PushSubscriptions.Remove(subscription);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
