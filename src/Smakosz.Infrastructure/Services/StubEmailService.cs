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

    public Task SendSecurityPasswordChangedAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Security password changed alert sent to {Email} (ip={Ip}, country={Country})", email, ipAddress, countryCode);
        return Task.CompletedTask;
    }

    public Task SendSecurityTwoFactorDisabledAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Security 2FA disabled alert sent to {Email} (ip={Ip}, country={Country})", email, ipAddress, countryCode);
        return Task.CompletedTask;
    }

    public Task SendSecurityAccountLockedAsync(string email, int failedAttempts, DateTime lockUntil, string? ipAddress, string? countryCode, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Security account locked alert sent to {Email} (attempts={Attempts}, until={Until}, ip={Ip}, country={Country})", email, failedAttempts, lockUntil, ipAddress, countryCode);
        return Task.CompletedTask;
    }

    public Task SendSecurityNewCountryLoginAsync(string email, string countryCode, string? ipAddress, string? userAgent, DateTime occurredAt, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Security new country login alert sent to {Email} (country={Country}, ip={Ip})", email, countryCode, ipAddress);
        return Task.CompletedTask;
    }
}
