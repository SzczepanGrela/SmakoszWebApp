using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.AcceptInvite;

public class AcceptInviteHandler : IRequestHandler<AcceptInviteCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICodeHasher _codeHasher;
    private readonly ICurrentUserService _currentUser;

    public AcceptInviteHandler(
        ISmakoszDbContext db,
        IPasswordHasher passwordHasher,
        ICodeHasher codeHasher,
        ICurrentUserService currentUser)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _codeHasher = codeHasher;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.Auth.InvalidVerificationCode;

        var verificationCode = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                vc => vc.UserId == user.UserId
                    && vc.Type == VerificationCodeType.Invitation
                    && vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (verificationCode is null || !_codeHasher.Verify(request.Code, verificationCode.CodeHash))
            return DomainErrors.Auth.InvalidVerificationCode;

        var now = DateTime.UtcNow;

        _db.VerificationCodes.Remove(verificationCode);
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAt = now;

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.PasswordChanged,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Email = user.Email,
            UserId = user.UserId,
            Details = JsonSerializer.Serialize(new { flow = "accept_invite" }),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
