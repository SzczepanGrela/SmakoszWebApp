namespace Smakosz.Application.Common.Interfaces;

public interface ISecurityNotificationService
{
    Task NotifyPasswordChangedAsync(int userId, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default);
    Task NotifyTwoFactorDisabledAsync(int userId, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default);
    Task NotifyAccountLockedAsync(int userId, int failedAttempts, DateTime lockUntil, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default);
    Task NotifyNewCountryLoginIfApplicableAsync(int userId, string? countryCode, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
