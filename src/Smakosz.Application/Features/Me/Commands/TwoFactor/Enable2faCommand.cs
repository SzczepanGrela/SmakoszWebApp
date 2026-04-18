using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.TwoFactor;

public record Enable2faCommand : IRequest<ErrorOr<Success>>;

public class Enable2faHandler : IRequestHandler<Enable2faCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;

    public Enable2faHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IVerificationCodeService verificationCodeService,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _verificationCodeService = verificationCodeService;
        _emailService = emailService;
    }

    public async Task<ErrorOr<Success>> Handle(Enable2faCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (user.Is2faEnabled)
            return DomainErrors.Auth.TwoFactorAlreadyEnabled;

        var code = await _verificationCodeService.CreateCodeAsync(
            user.UserId, VerificationCodeType.TwoFactorAuth, cancellationToken);

        await _emailService.Send2faCodeAsync(user.Email, code, cancellationToken);

        _db.EmailLogs.Add(new EmailLog
        {
            Type = "TwoFactorAuth",
            Recipient = user.Email,
            Subject = "Kod 2FA",
            Status = "sent",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
