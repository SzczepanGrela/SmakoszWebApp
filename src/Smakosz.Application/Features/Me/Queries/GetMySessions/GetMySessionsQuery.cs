using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMySessions;

public record GetMySessionsQuery() : IRequest<ErrorOr<List<SessionDto>>>;

public class GetMySessionsHandler : IRequestHandler<GetMySessionsQuery, ErrorOr<List<SessionDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMySessionsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<SessionDto>>> Handle(GetMySessionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var sessions = await _db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId.Value && s.ExpiresAt > DateTime.UtcNow && !s.IsRevoked)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionDto
            {
                SessionId = s.UserSessionId,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                IsCurrent = s.UserSessionId == _currentUser.SessionId
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }
}
