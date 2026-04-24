using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.AdminResetUserPassword;

public record AdminResetUserPasswordCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class AdminResetUserPasswordHandler : IRequestHandler<AdminResetUserPasswordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;

    public AdminResetUserPasswordHandler(
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

    public async Task<ErrorOr<Success>> Handle(AdminResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var code = await _verificationCodeService.CreateCodeAsync(user.UserId, VerificationCodeType.ResetPassword, cancellationToken);

        await _emailService.SendPasswordResetAsync(user.Email, code, cancellationToken);

        _db.EmailLogs.Add(new EmailLog
        {
            Type = "PasswordReset",
            Recipient = user.Email,
            Subject = "Reset hasła",
            Status = "sent",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        });

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.PasswordResetByAdmin,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Email = user.Email,
            UserId = user.UserId,
            Details = $"{{\"admin_id\": {_currentUser.UserId}}}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
