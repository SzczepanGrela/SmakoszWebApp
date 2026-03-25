using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Resend2fa;

public class Resend2faHandler : IRequestHandler<Resend2faCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;

    public Resend2faHandler(ISmakoszDbContext db, IEmailService emailService, IPasswordHasher passwordHasher)
    {
        _db = db;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
    }

    public async Task<ErrorOr<Success>> Handle(Resend2faCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant()
                && !u.IsDeleted
                && u.Is2faEnabled, cancellationToken);

        // Silent success for security
        if (user is null)
            return Result.Success;

        var code = GenerateCode();

        var verificationCode = new VerificationCode
        {
            UserId = user.UserId,
            CodeHash = _passwordHasher.Hash(code),
            Type = VerificationCodeType.TwoFactorAuth,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _db.VerificationCodes.Add(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

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

    private static string GenerateCode() => Random.Shared.Next(100000, 999999).ToString();
}
