using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;

public class CreatePrivilegedAccountHandler : IRequestHandler<CreatePrivilegedAccountCommand, ErrorOr<Guid>>
{
    private static readonly TimeSpan InviteTtl = TimeSpan.FromHours(24);

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;

    public CreatePrivilegedAccountHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IEmailService emailService,
        IVerificationCodeService verificationCodeService)
    {
        _db = db;
        _currentUser = currentUser;
        _emailService = emailService;
        _verificationCodeService = verificationCodeService;
    }

    public async Task<ErrorOr<Guid>> Handle(CreatePrivilegedAccountCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (request.Role != UserRole.Admin && request.Role != UserRole.Moderator)
            return DomainErrors.Admin.InvalidRoleForPrivilegedAccount;

        var normalizedEmail = request.Email.ToLowerInvariant();

        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailExists)
            return DomainErrors.Admin.EmailAlreadyExists;

        var usernameExists = await _db.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (usernameExists)
            return DomainErrors.Admin.UsernameAlreadyExists;

        var now = DateTime.UtcNow;

        var user = new User
        {
            Username = request.Username,
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            Role = request.Role,
            IsActive = true,
            EmailVerified = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var code = await _verificationCodeService.CreateCodeAsync(user.UserId, VerificationCodeType.Invitation, InviteTtl, cancellationToken);

        await _emailService.SendInvitationAsync(user.Email, code, user.Username, user.Role, cancellationToken);

        _db.EmailLogs.Add(new EmailLog
        {
            Type = "Invitation",
            Recipient = user.Email,
            Subject = "Smakosz - zaproszenie do zespołu",
            Status = "sent",
            CreatedAt = now,
            SentAt = now
        });

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.AccountInvited,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Email = user.Email,
            UserId = user.UserId,
            Details = JsonSerializer.Serialize(new { admin_id = _currentUser.UserId, role = user.Role.ToString() }),
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "users",
            RecordId = user.UserId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            NewValues = JsonSerializer.Serialize(new
            {
                user.Username,
                user.Email,
                Role = user.Role.ToString(),
                user.PublicId
            })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return user.PublicId;
    }
}
