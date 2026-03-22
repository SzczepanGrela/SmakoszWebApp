using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly ITurnstileService _turnstile;

    public ForgotPasswordHandler(ISmakoszDbContext db, IEmailService emailService, IVerificationCodeService verificationCodeService, ITurnstileService turnstile)
    {
        _db = db;
        _emailService = emailService;
        _verificationCodeService = verificationCodeService;
        _turnstile = turnstile;
    }

    public async Task<ErrorOr<Success>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!await _turnstile.VerifyAsync(request.TurnstileToken ?? string.Empty, cancellationToken))
        {
            return DomainErrors.Captcha.VerificationFailed;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result.Success;

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
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
