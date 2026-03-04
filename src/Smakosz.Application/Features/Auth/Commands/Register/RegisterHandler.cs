using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICodeHasher _codeHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;

    public RegisterHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, ICodeHasher codeHasher, ICurrentUserService currentUser, IEmailService emailService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _codeHasher = codeHasher;
        _currentUser = currentUser;
        _emailService = emailService;
    }

    public async Task<ErrorOr<Success>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (emailExists)
            return DomainErrors.Auth.EmailAlreadyExists;

        var usernameExists = await _db.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
            return DomainErrors.Auth.UsernameAlreadyExists;

        var usernameLower = request.Username.ToLowerInvariant();
        var forbiddenUsername = await _db.ForbiddenWords
            .Where(fw => fw.Category == ForbiddenWordCategory.Reserved || fw.Category == ForbiddenWordCategory.Offensive)
            .AnyAsync(fw => !fw.IsRegex && usernameLower.Contains(fw.Word.ToLower()), cancellationToken);

        if (forbiddenUsername)
            return DomainErrors.ForbiddenWord.UsernameContainsForbiddenWord;

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

        // Generate and send verification code
        var code = Random.Shared.Next(100000, 999999).ToString();
        _db.VerificationCodes.Add(new VerificationCode
        {
            UserId = user.UserId,
            CodeHash = _codeHasher.Hash(code),
            Type = VerificationCodeType.Register,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationCodeAsync(user.Email, code, cancellationToken);

        _db.EmailLogs.Add(new EmailLog
        {
            Type = "Verification",
            Recipient = user.Email,
            Subject = "Weryfikacja email",
            Status = "sent",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
