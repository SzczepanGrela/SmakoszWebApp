using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Jobs;

public class R2CleanupService
{
    private readonly ISmakoszDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<R2CleanupService> _logger;

    public R2CleanupService(
        ISmakoszDbContext db,
        IFileStorageService storage,
        IDateTimeProvider clock,
        ILogger<R2CleanupService> logger)
    {
        _db = db;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        // Seed assets are shared across many users; never delete them from R2 even if a stale enqueue slipped through.
        var batch = await _db.FilesToDelete
            .Where(f => f.ProcessedAt == null && !f.R2Key.StartsWith("seed/"))
            .OrderBy(f => f.QueuedAt)
            .Take(50)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return;

        var now = _clock.UtcNow;
        var deleted = 0;

        foreach (var file in batch)
        {
            try
            {
                await _storage.DeleteAsync(file.R2Key, ct);
                file.ProcessedAt = now;
                file.Error = null;
                deleted++;
            }
            catch (Exception ex)
            {
                file.Error = ex.Message;
                _logger.LogWarning(ex, "r2-cleanup: failed to delete {Key}", file.R2Key);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("r2-cleanup: deleted {Deleted}/{Total} files", deleted, batch.Count);
    }
}
