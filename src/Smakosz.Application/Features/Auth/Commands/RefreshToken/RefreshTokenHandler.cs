using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;

namespace Smakosz.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenHandler(ISmakoszDbContext db, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(
                s => s.RefreshTokenHash == request.RefreshToken && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (session is null)
            return DomainErrors.Auth.InvalidRefreshToken;

        var user = session.User;

        if (user.IsDeleted || !user.IsActive || user.IsBanned)
            return DomainErrors.Auth.InvalidRefreshToken;

        session.IsRevoked = true;

        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newSession = new Domain.Entities.UserSession
        {
            UserId = user.UserId,
            RefreshTokenHash = newRefreshToken,
            ExpiresAt = session.ExpiresAt
        };

        _db.UserSessions.Add(newSession);
        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
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
