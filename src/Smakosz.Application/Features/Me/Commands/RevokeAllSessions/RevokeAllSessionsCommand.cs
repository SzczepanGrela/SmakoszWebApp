using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.RevokeAllSessions;

public record RevokeAllSessionsCommand() : IRequest<ErrorOr<Success>>;

public class RevokeAllSessionsHandler : IRequestHandler<RevokeAllSessionsCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RevokeAllSessionsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (!_currentUser.SessionId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == _currentUser.UserId.Value
                && s.UserSessionId != _currentUser.SessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            _db.UserSessions.Remove(session);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
