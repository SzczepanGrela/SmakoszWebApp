using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Auth.Commands.Logout;

public class LogoutHandler : IRequestHandler<LogoutCommand, ErrorOr<Deleted>>
{
    private readonly ISmakoszDbContext _db;

    public LogoutHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<Deleted>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(
                s => s.RefreshTokenHash == request.RefreshToken && !s.IsRevoked,
                cancellationToken);

        if (session is not null)
        {
            session.IsRevoked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Deleted;
    }
}
