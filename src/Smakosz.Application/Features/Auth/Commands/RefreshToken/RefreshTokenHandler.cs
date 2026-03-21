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

    public RefreshTokenHandler(ISmakoszDbContext db, IJwtTokenService jwtTokenService, ISessionService sessionService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _sessionService = sessionService;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.FindActiveSessionAsync(request.RefreshToken, cancellationToken);

        if (session is null)
            return DomainErrors.Auth.InvalidRefreshToken;

        var user = session.User;

        if (user.IsDeleted || !user.IsActive || user.IsBanned)
            return DomainErrors.Auth.InvalidRefreshToken;

        var newRefreshToken = await _sessionService.RotateSessionAsync(session, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(accessTtl),
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
