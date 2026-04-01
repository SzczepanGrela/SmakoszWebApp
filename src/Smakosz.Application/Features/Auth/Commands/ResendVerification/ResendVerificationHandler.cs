using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.ResendVerification;

public class ResendVerificationHandler : IRequestHandler<ResendVerificationCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ICodeHasher _codeHasher;

    public ResendVerificationHandler(ISmakoszDbContext db, IEmailService emailService, ICodeHasher codeHasher)
    {
        _db = db;
        _emailService = emailService;
        _codeHasher = codeHasher;
    }

    public async Task<ErrorOr<Success>> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null || user.EmailVerified)
            return Result.Success;

        var oldCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == user.UserId && vc.Type == VerificationCodeType.Register)
            .ToListAsync(cancellationToken);
        _db.VerificationCodes.RemoveRange(oldCodes);

        var code = GenerateCode();

        var verificationCode = new VerificationCode
        {
            UserId = user.UserId,
            CodeHash = _codeHasher.Hash(code),
            Type = VerificationCodeType.Register,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        _db.VerificationCodes.Add(verificationCode);
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

    private static string GenerateCode() => Random.Shared.Next(100000, 999999).ToString();
}
