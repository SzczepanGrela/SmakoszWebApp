using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class LogRetentionService
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LogRetentionService> _logger;

    public LogRetentionService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<LogRetentionService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var security = await TryCleanupAsync("security", CleanupSecurityLogsAsync, ct);
        var audit = await TryCleanupAsync("audit", CleanupAuditLogsAsync, ct);
        var email = await TryCleanupAsync("email", CleanupEmailLogsAsync, ct);
        var moderation = await TryCleanupAsync("moderation", CleanupModerationLogsAsync, ct);
        var ai = await TryCleanupAsync("ai", CleanupAiLogsAsync, ct);

        _logger.LogInformation(
            "log-retention: deleted {Security} security, {Audit} audit, {Email} email, {Moderation} moderation, {Ai} ai log entries",
            security, audit, email, moderation, ai);
    }

    private async Task<int> CleanupSecurityLogsAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.security_logs_days", 90, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var entries = await _db.SecurityLogs.Where(l => l.CreatedAt < cutoff).ToListAsync(ct);
        if (entries.Count == 0) return 0;
        _db.SecurityLogs.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task<int> CleanupAuditLogsAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.audit_logs_days", 365, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var entries = await _db.AuditLogs.Where(l => l.ChangedAt < cutoff).ToListAsync(ct);
        if (entries.Count == 0) return 0;
        _db.AuditLogs.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task<int> CleanupEmailLogsAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.email_logs_days", 60, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var entries = await _db.EmailLogs.Where(l => l.CreatedAt < cutoff).ToListAsync(ct);
        if (entries.Count == 0) return 0;
        _db.EmailLogs.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task<int> CleanupModerationLogsAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.moderation_logs_days", 180, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var entries = await _db.ModerationLogs.Where(l => l.CreatedAt < cutoff).ToListAsync(ct);
        if (entries.Count == 0) return 0;
        _db.ModerationLogs.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task<int> CleanupAiLogsAsync(CancellationToken ct)
    {
        var days = await GetIntConfigAsync("retention.ai_logs_days", 30, ct);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var entries = await _db.AiLogs.Where(l => l.CreatedAt < cutoff).ToListAsync(ct);
        if (entries.Count == 0) return 0;
        _db.AiLogs.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task<int> TryCleanupAsync(string name, Func<CancellationToken, Task<int>> operation, CancellationToken ct)
    {
        try
        {
            return await operation(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "log-retention: failed to clean {Name} logs", name);
            return 0;
        }
    }

    private async Task<int> GetIntConfigAsync(string key, int defaultValue, CancellationToken ct)
    {
        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);
        return config is not null && int.TryParse(config.Value, out var v) ? v : defaultValue;
    }
}
