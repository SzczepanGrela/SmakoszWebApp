using Smakosz.Application.Common.Models;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default);
    Task Send2faCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendContactConfirmationAsync(string email, string contactName, string subject, CancellationToken ct = default);
    Task SendContactResponseAsync(string email, string responseText, CancellationToken ct = default);
    Task SendNotificationDigestAsync(string email, string subject, IReadOnlyList<NotificationItem> notifications, CancellationToken ct = default);
    Task SendAccountDeletionCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendAccountDeletionConfirmationAsync(string email, CancellationToken ct = default);
    Task SendInvitationAsync(string email, string code, string username, UserRole role, CancellationToken ct = default);
    Task SendSecurityPasswordChangedAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default);
    Task SendSecurityTwoFactorDisabledAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default);
    Task SendSecurityAccountLockedAsync(string email, int failedAttempts, DateTime lockUntil, string? ipAddress, string? countryCode, CancellationToken ct = default);
    Task SendSecurityNewCountryLoginAsync(string email, string countryCode, string? ipAddress, string? userAgent, DateTime occurredAt, CancellationToken ct = default);
}
