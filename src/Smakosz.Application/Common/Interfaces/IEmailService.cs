namespace Smakosz.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default);
    Task Send2faCodeAsync(string email, string code, CancellationToken ct = default);
}
