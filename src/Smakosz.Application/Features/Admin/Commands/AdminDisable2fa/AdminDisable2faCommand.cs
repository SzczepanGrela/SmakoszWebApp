using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.AdminDisable2fa;

public record AdminDisable2faCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class AdminDisable2faHandler : IRequestHandler<AdminDisable2faCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminDisable2faHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AdminDisable2faCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (!user.Is2faEnabled)
            return DomainErrors.Auth.TwoFactorNotEnabled;

        user.Is2faEnabled = false;

        var pendingCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == user.UserId && vc.Type == VerificationCodeType.TwoFactorAuth)
            .ToListAsync(cancellationToken);
        _db.VerificationCodes.RemoveRange(pendingCodes);

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.TwoFactorDisabledByAdmin,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            CountryCode = _currentUser.CountryCode,
            Email = user.Email,
            UserId = user.UserId,
            Details = SecurityLogDetails.AdminAction(_currentUser.UserId),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
