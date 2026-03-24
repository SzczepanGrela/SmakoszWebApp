using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.ResendVerification;

public class ResendVerificationHandler : IRequestHandler<ResendVerificationCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;

    public ResendVerificationHandler(ISmakoszDbContext db, IEmailService emailService, IPasswordHasher passwordHasher)
    {
        _db = db;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
    }

    public async Task<ErrorOr<Success>> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        // Silent success for security - don't reveal if email exists
        if (user is null || user.EmailVerified)
            return Result.Success;

        var code = GenerateCode();

        var verificationCode = new VerificationCode
        {
            UserId = user.UserId,
            CodeHash = _passwordHasher.Hash(code),
            Type = VerificationCodeType.Register,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        _db.VerificationCodes.Add(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationCodeAsync(user.Email, code, cancellationToken);

        return Result.Success;
    }

    private static string GenerateCode() => Random.Shared.Next(100000, 999999).ToString();
}
