using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class SessionCleanupService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<SessionCleanupService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var sessions = await _db.UserSessions
            .Where(s => s.ExpiresAt < now || s.IsRevoked)
            .ExecuteDeleteAsync(ct);

        var tokens = await _db.RefreshTokens
            .Where(t => t.ExpiresAt < now || t.RevokedAt != null)
            .ExecuteDeleteAsync(ct);

        var codes = await _db.VerificationCodes
            .Where(c => c.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "session-cleanup: deleted {Sessions} sessions, {Tokens} refresh tokens, {Codes} verification codes",
            sessions, tokens, codes);
    }
}
