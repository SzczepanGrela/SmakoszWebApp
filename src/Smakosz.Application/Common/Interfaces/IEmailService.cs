using Smakosz.Application.Common.Models;

namespace Smakosz.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default);
    Task Send2faCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendContactConfirmationAsync(string email, string contactName, string subject, CancellationToken ct = default);
    Task SendContactResponseAsync(string email, string responseText, CancellationToken ct = default);
    Task SendNotificationDigestAsync(string email, string subject, IReadOnlyList<NotificationItem> notifications, CancellationToken ct = default);
}
