using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly ISmakoszDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public SessionService(ISmakoszDbContext db, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<SessionTokenResult> CreateSessionAsync(int userId, bool rememberMe, CancellationToken ct)
    {
        var plaintext = _jwtTokenService.GenerateRefreshToken();
        var hash = HashToken(plaintext);

        var ttlDays = rememberMe
            ? await GetIntConfigAsync("auth.refresh_ttl_days_remember", 30, ct)
            : await GetIntConfigAsync("auth.refresh_ttl_days", 7, ct);

        var expiresAt = DateTime.UtcNow.AddDays(ttlDays);
        var session = new UserSession
        {
            UserId = userId,
            RefreshTokenHash = hash,
            IsRememberMe = rememberMe,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        };

        _db.UserSessions.Add(session);
        return new SessionTokenResult(plaintext, expiresAt);
    }

    public async Task<UserSession?> FindActiveSessionAsync(string refreshToken, CancellationToken ct)
    {
        var hash = HashToken(refreshToken);
        return await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(
                s => s.RefreshTokenHash == hash && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow,
                ct);
    }

    public async Task<UserSession?> FindSessionForLogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = HashToken(refreshToken);
        return await _db.UserSessions
            .FirstOrDefaultAsync(
                s => s.RefreshTokenHash == hash && !s.IsRevoked,
                ct);
    }

    public void RevokeSession(UserSession session)
    {
        session.IsRevoked = true;
    }

    public async Task<SessionTokenResult> RotateSessionAsync(UserSession oldSession, CancellationToken ct)
    {
        oldSession.IsRevoked = true;

        var plaintext = _jwtTokenService.GenerateRefreshToken();
        var hash = HashToken(plaintext);

        var ttlDays = oldSession.IsRememberMe
            ? await GetIntConfigAsync("auth.refresh_ttl_days_remember", 30, ct)
            : await GetIntConfigAsync("auth.refresh_ttl_days", 7, ct);

        var expiresAt = DateTime.UtcNow.AddDays(ttlDays);
        var newSession = new UserSession
        {
            UserId = oldSession.UserId,
            RefreshTokenHash = hash,
            IsRememberMe = oldSession.IsRememberMe,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        };

        _db.UserSessions.Add(newSession);
        return new SessionTokenResult(plaintext, expiresAt);
    }

    public async Task<int> GetAccessTokenLifetimeSecondsAsync(CancellationToken ct)
    {
        return await GetIntConfigAsync("auth.access_ttl_sec", 900, ct);
    }

    internal static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var value = await _db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);
        return value is not null && int.TryParse(value, out var result) ? result : defaultValue;
    }
}
