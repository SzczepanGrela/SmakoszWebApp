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
    private readonly ICodeHasher _codeHasher;
    private readonly ITurnstileService _turnstile;

    public ForgotPasswordHandler(ISmakoszDbContext db, IEmailService emailService, ICodeHasher codeHasher, ITurnstileService turnstile)
    {
        _db = db;
        _emailService = emailService;
        _codeHasher = codeHasher;
        _turnstile = turnstile;
    }

    public async Task<ErrorOr<Success>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.TurnstileToken) ||
            !await _turnstile.VerifyAsync(request.TurnstileToken, cancellationToken))
        {
            return DomainErrors.Captcha.VerificationFailed;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result.Success;

        var code = GenerateCode();

        var verificationCode = new VerificationCode
        {
            UserId = user.UserId,
            CodeHash = _codeHasher.Hash(code),
            Type = VerificationCodeType.ResetPassword,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        _db.VerificationCodes.Add(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

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

    private static string GenerateCode() => Random.Shared.Next(100000, 999999).ToString();
}
