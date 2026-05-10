using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;

namespace Smakosz.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISessionService _sessionService;
    private readonly IBusinessMetrics _metrics;

    public RefreshTokenHandler(ISmakoszDbContext db, IJwtTokenService jwtTokenService, ISessionService sessionService, IBusinessMetrics metrics)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _sessionService = sessionService;
        _metrics = metrics;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.FindActiveSessionAsync(request.RefreshToken, cancellationToken);

        if (session is null)
        {
            _metrics.RecordJwtRefresh("invalid_session");
            return DomainErrors.Auth.InvalidRefreshToken;
        }

        var user = session.User;

        if (user.IsDeleted || !user.IsActive || user.IsBanned)
        {
            _metrics.RecordJwtRefresh("user_inactive");
            return DomainErrors.Auth.InvalidRefreshToken;
        }

        var rotated = await _sessionService.RotateSessionAsync(session, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        await _db.SaveChangesAsync(cancellationToken);

        _metrics.RecordJwtRefresh("success");

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = rotated.Token,
            ExpiresAt = DateTime.UtcNow.AddSeconds(accessTtl),
            RefreshTokenExpiresAt = rotated.ExpiresAt,
            User = new UserProfileDto
            {
                PublicId = user.PublicId,
                Slug = user.Slug ?? string.Empty,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role.ToString(),
                EmailVerified = user.EmailVerified
            }
        };
    }
}
