using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;

    public RegisterHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ICurrentUserService currentUser)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (emailExists)
            return DomainErrors.Auth.EmailAlreadyExists;

        var usernameExists = await _db.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
            return DomainErrors.Auth.UsernameAlreadyExists;

        var emailDomain = request.Email.ToLowerInvariant().Split('@')[1];
        var ipAddress = _currentUser.IpAddress;
        var now = DateTime.UtcNow;

        var isBanned = await _db.BannedIdentifiers.AnyAsync(b =>
            (b.ExpiresAt == null || b.ExpiresAt > now) &&
            (
                (b.Type == BannedIdentifierType.Email && b.Value == request.Email.ToLowerInvariant()) ||
                (b.Type == BannedIdentifierType.EmailDomain && b.Value == emailDomain) ||
                (b.Type == BannedIdentifierType.Ip && ipAddress != null && b.Value == ipAddress)
            ), cancellationToken);

        if (isBanned)
        {
            _db.SecurityLogs.Add(new SecurityLog
            {
                EventType = SecurityEventType.BannedRegistration,
                IpAddress = ipAddress,
                UserAgent = _currentUser.UserAgent,
                Email = request.Email.ToLowerInvariant(),
                Details = "{\"reason\": \"banned_identifier\"}",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            return DomainErrors.Auth.IdentifierBanned;
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.User,
            IsActive = true,
            EmailVerified = false,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        var session = new UserSession
        {
            UserId = user.UserId,
            RefreshTokenHash = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.UserSessions.Add(session);
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
