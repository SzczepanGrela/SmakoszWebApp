using Smakosz.Domain.Entities;

namespace Smakosz.Application.Common.Interfaces;

public record SessionTokenResult(string Token, DateTime ExpiresAt);

public interface ISessionService
{
    Task<SessionTokenResult> CreateSessionAsync(int userId, bool rememberMe, CancellationToken ct);
    Task<UserSession?> FindActiveSessionAsync(string refreshToken, CancellationToken ct);
    Task<UserSession?> FindSessionForLogoutAsync(string refreshToken, CancellationToken ct);
    void RevokeSession(UserSession session);
    Task<SessionTokenResult> RotateSessionAsync(UserSession oldSession, CancellationToken ct);
    Task<int> GetAccessTokenLifetimeSecondsAsync(CancellationToken ct);
}
