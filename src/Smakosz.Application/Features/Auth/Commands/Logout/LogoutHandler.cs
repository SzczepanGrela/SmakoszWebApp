using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Auth.Commands.Logout;

public class LogoutHandler : IRequestHandler<LogoutCommand, ErrorOr<Deleted>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ISessionService _sessionService;

    public LogoutHandler(ISmakoszDbContext db, ISessionService sessionService)
    {
        _db = db;
        _sessionService = sessionService;
    }

    public async Task<ErrorOr<Deleted>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.FindSessionForLogoutAsync(request.RefreshToken, cancellationToken);

        if (session is not null)
        {
            _sessionService.RevokeSession(session);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Deleted;
    }
}
