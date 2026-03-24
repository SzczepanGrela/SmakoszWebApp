using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Verify2fa;

public class Verify2faHandler : IRequestHandler<Verify2faCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public Verify2faHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(Verify2faCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.Auth.InvalidCredentials;

        var verificationCode = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                vc => vc.UserId == user.UserId
                    && vc.Type == VerificationCodeType.TwoFactorAuth
                    && vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (verificationCode is null || !_passwordHasher.Verify(request.Code, verificationCode.CodeHash))
            return DomainErrors.Auth.InvalidVerificationCode;

        // Remove used code
        _db.VerificationCodes.Remove(verificationCode);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        var session = new UserSession
        {
            UserId = user.UserId,
            RefreshTokenHash = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.UserSessions.Add(session);
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
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
