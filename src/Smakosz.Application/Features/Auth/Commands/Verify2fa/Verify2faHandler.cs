using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Verify2fa;

public class Verify2faHandler : IRequestHandler<Verify2faCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICodeHasher _codeHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISessionService _sessionService;

    public Verify2faHandler(ISmakoszDbContext db, ICodeHasher codeHasher, IJwtTokenService jwtTokenService, ISessionService sessionService)
    {
        _db = db;
        _codeHasher = codeHasher;
        _jwtTokenService = jwtTokenService;
        _sessionService = sessionService;
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

        var maxAttempts = await GetMaxAttemptsAsync(cancellationToken);

        if (verificationCode is null || verificationCode.AttemptsCount >= maxAttempts || !_codeHasher.Verify(request.Code, verificationCode.CodeHash))
        {
            if (verificationCode is not null)
            {
                verificationCode.AttemptsCount++;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return DomainErrors.Auth.InvalidVerificationCode;
        }

        _db.VerificationCodes.Remove(verificationCode);

        var refreshToken = await _sessionService.CreateSessionAsync(user.UserId, false, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
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

    private async Task<int> GetMaxAttemptsAsync(CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == "auth.verify_code_max_attempts", ct);
        return config is not null && int.TryParse(config.Value, out var v) && v > 0 ? v : 3;
    }
}
