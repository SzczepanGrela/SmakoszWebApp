using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.RevokeSession;

public record RevokeSessionCommand(long SessionId) : IRequest<ErrorOr<Success>>;

public class RevokeSessionHandler : IRequestHandler<RevokeSessionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RevokeSessionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (!_currentUser.SessionId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (request.SessionId == _currentUser.SessionId)
            return DomainErrors.Session.CannotRevokeCurrent;

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(
                s => s.UserSessionId == request.SessionId && s.UserId == _currentUser.UserId.Value,
                cancellationToken);

        if (session is null)
            return DomainErrors.Session.NotFound;

        _db.UserSessions.Remove(session);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
