using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.DeleteAccount;

public record ConfirmAccountDeletionCommand(string Code) : IRequest<ErrorOr<Success>>;

public class ConfirmAccountDeletionHandler : IRequestHandler<ConfirmAccountDeletionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodeHasher _codeHasher;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailService _emailService;

    public ConfirmAccountDeletionHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        ICodeHasher codeHasher,
        IDateTimeProvider clock,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _codeHasher = codeHasher;
        _clock = clock;
        _emailService = emailService;
    }

    public async Task<ErrorOr<Success>> Handle(ConfirmAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;
        var now = _clock.UtcNow;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var originalEmail = user.Email;
        var originalAvatarUrl = user.AvatarUrl;

        var verificationCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == userId
                && vc.Type == VerificationCodeType.AccountDeletion
                && vc.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var maxAttempts = await GetMaxAttemptsAsync(cancellationToken);

        var verificationCode = verificationCodes
            .FirstOrDefault(vc => vc.AttemptsCount < maxAttempts && _codeHasher.Verify(request.Code, vc.CodeHash));

        if (verificationCode is null)
        {
            foreach (var vc in verificationCodes)
            {
                vc.AttemptsCount++;
            }
            await _db.SaveChangesAsync(cancellationToken);
            return DomainErrors.Auth.InvalidVerificationCode;
        }

        _db.VerificationCodes.Remove(verificationCode);

        var pseudoHash = ComputePseudoHash(userId, originalEmail);
        var hash8 = pseudoHash[..8];

        user.Username = $"usuniety_{hash8}";
        user.Email = pseudoHash;
        user.Slug = $"usuniety-{hash8}";
        user.FirstName = null;
        user.LastName = null;
        user.FullName = null;
        user.Phone = null;
        user.AvatarUrl = null;
        user.AvatarBlurhash = null;
        user.PasswordHash = "";
        user.SecurityStamp = Guid.NewGuid().ToString();

        user.IsDeleted = true;
        user.DeletedAt = now;
        user.IsActive = false;

        var reviews = await _db.Reviews
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var review in reviews)
        {
            review.IsDeleted = true;
            review.DeletedAt = now;
        }

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _db.UserSessions.RemoveRange(sessions);

        var pushSubs = await _db.PushSubscriptions
            .Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        _db.PushSubscriptions.RemoveRange(pushSubs);

        var remainingCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == userId).ToListAsync(cancellationToken);
        _db.VerificationCodes.RemoveRange(remainingCodes);

        var searchHistory = await _db.SearchHistories
            .Where(sh => sh.UserId == userId).ToListAsync(cancellationToken);
        _db.SearchHistories.RemoveRange(searchHistory);

        var notifSettings = await _db.UserNotificationSettings
            .Where(uns => uns.UserId == userId).ToListAsync(cancellationToken);
        _db.UserNotificationSettings.RemoveRange(notifSettings);

        if (originalAvatarUrl is not null)
        {
            _db.FilesToDelete.Add(new FileToDelete
            {
                R2Key = ExtractR2Key(originalAvatarUrl),
                Bucket = "smakosz-photos",
                Reason = "gdpr_account_deletion",
                SourceEntity = "User",
                SourceId = userId,
                QueuedAt = now
            });
        }

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.AccountDeleted,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Email = originalEmail,
            UserId = userId,
            Details = "{\"action\":\"gdpr_account_deletion\"}",
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Users",
            RecordId = userId,
            Operation = AuditOperation.Delete,
            ChangedBy = userId.ToString(),
            ChangedAt = now,
            NewValues = "{\"action\":\"gdpr_account_deletion\"}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendAccountDeletionConfirmationAsync(originalEmail, cancellationToken);

        return Result.Success;
    }

    private async Task<int> GetMaxAttemptsAsync(CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == "auth.verify_code_max_attempts", ct);
        return config is not null && int.TryParse(config.Value, out var v) && v > 0 ? v : 3;
    }

    private static string ComputePseudoHash(int userId, string email)
    {
        var input = $"{userId}:{email}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static string ExtractR2Key(string url) => new Uri(url).AbsolutePath.TrimStart('/');
}
