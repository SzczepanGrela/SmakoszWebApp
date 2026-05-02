using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger) => _logger = logger;

    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Verification code {Code} sent to {Email}", code, email);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Password reset code {Code} sent to {Email}", code, email);
        return Task.CompletedTask;
    }

    public Task Send2faCodeAsync(string email, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] 2FA code {Code} sent to {Email}", code, email);
        return Task.CompletedTask;
    }

    public Task SendContactConfirmationAsync(string email, string contactName, string subject, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Contact confirmation sent to {Email} (name={Name}, subject={Subject})", email, contactName, subject);
        return Task.CompletedTask;
    }

    public Task SendContactResponseAsync(string email, string responseText, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Contact response sent to {Email}", email);
        return Task.CompletedTask;
    }

    public Task SendNotificationDigestAsync(string email, string subject, IReadOnlyList<NotificationItem> notifications, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Notification digest ({Count} items) sent to {Email}: {Subject}", notifications.Count, email, subject);
        return Task.CompletedTask;
    }

    public Task SendAccountDeletionCodeAsync(string email, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Account deletion code {Code} sent to {Email}", code, email);
        return Task.CompletedTask;
    }

    public Task SendAccountDeletionConfirmationAsync(string email, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Account deletion confirmation sent to {Email}", email);
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(string email, string code, string username, UserRole role, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Invitation code {Code} for role {Role} sent to {Email} (username={Username})", code, role, email, username);
        return Task.CompletedTask;
    }
}
