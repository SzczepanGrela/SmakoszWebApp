using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

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

    public Task SendDigestAsync(string email, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email Stub] Digest to {Email}: {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
