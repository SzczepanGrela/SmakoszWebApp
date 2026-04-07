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
    private readonly IVerificationCodeService _verificationCodeService;

    public Resend2faHandler(ISmakoszDbContext db, IEmailService emailService, IVerificationCodeService verificationCodeService)
    {
        _db = db;
        _emailService = emailService;
        _verificationCodeService = verificationCodeService;
    }

    public async Task<ErrorOr<Success>> Handle(Resend2faCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant()
                && !u.IsDeleted
                && u.Is2faEnabled, cancellationToken);

        if (user is null)
            return Result.Success;

        var code = await _verificationCodeService.CreateCodeAsync(user.UserId, VerificationCodeType.TwoFactorAuth, cancellationToken);

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
