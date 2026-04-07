using Smakosz.Domain.Entities;

namespace Smakosz.Application.Common.Interfaces;

public interface ISessionService
{
    Task<string> CreateSessionAsync(int userId, bool rememberMe, CancellationToken ct);
    Task<UserSession?> FindActiveSessionAsync(string refreshToken, CancellationToken ct);
    Task<UserSession?> FindSessionForLogoutAsync(string refreshToken, CancellationToken ct);
    void RevokeSession(UserSession session);
    Task<string> RotateSessionAsync(UserSession oldSession, CancellationToken ct);
    Task<int> GetAccessTokenLifetimeSecondsAsync(CancellationToken ct);
}
