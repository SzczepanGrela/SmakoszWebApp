using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Infrastructure.Services;

public class UserRecommendationCacheRegenerationService
{
    private const int CacheSize = 12;
    private const int CommitBatch = 500;

    private readonly ISmakoszDbContext _db;
    private readonly IRecommendationProvider _provider;
    private readonly ILogger<UserRecommendationCacheRegenerationService> _logger;

    public UserRecommendationCacheRegenerationService(
        ISmakoszDbContext db,
        IRecommendationProvider provider,
        ILogger<UserRecommendationCacheRegenerationService> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    public async Task RegenerateAsync(string modelVersion, CancellationToken ct)
    {
        if (!_provider.IsAvailable)
        {
            _logger.LogWarning("Cache regen skipped: ONNX provider unavailable");
            return;
        }

        var loadedVersion = _provider.GetLoadedVersion();
        if (loadedVersion != modelVersion)
        {
            _logger.LogInformation(
                "Cache regen skipped: requested={Requested} but loaded={Loaded}",
                modelVersion, loadedVersion);
            return;
        }

        var alreadyPopulated = await _db.UserRecommendationCaches
            .AnyAsync(c => c.ModelVersion == modelVersion, ct);
        if (alreadyPopulated)
        {
            _logger.LogInformation("Cache already populated for version {Version}, skip", modelVersion);
            return;
        }

        var userIds = _provider.GetMappedUserIds();
        _logger.LogInformation(
            "Regenerating recommendation cache for {Count} users, version={Version}",
            userIds.Count, modelVersion);

        var sw = Stopwatch.StartNew();
        var processed = 0;

        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();

            var reviewedSet = (await _db.Reviews
                .Where(r => r.UserId == userId && !r.IsDeleted)
                .Select(r => r.DishId)
                .ToListAsync(ct)).ToHashSet();

            var topN = await _provider.GetPersonalizedAsync(userId, CacheSize + reviewedSet.Count, ct);
            var filtered = topN
                .Where(t => !reviewedSet.Contains(t.DishId))
                .Take(CacheSize)
                .ToList();

            if (filtered.Count == 0)
                continue;

            var entries = filtered.Select(x => new CachedDishEntry(x.DishId, x.Score)).ToList();
            var json = JsonSerializer.Serialize(entries);

            var existing = await _db.UserRecommendationCaches
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (existing is null)
            {
                _db.UserRecommendationCaches.Add(new UserRecommendationCache
                {
                    UserId = userId,
                    TopDishIdsJson = json,
                    ModelVersion = modelVersion,
                    GeneratedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.TopDishIdsJson = json;
                existing.ModelVersion = modelVersion;
                existing.GeneratedAt = DateTime.UtcNow;
            }

            processed++;
            if (processed % CommitBatch == 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Cache regen progress: {Done}/{Total} users ({Elapsed:F1}s)",
                    processed, userIds.Count, sw.Elapsed.TotalSeconds);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Cache regen done: {Done}/{Total} users in {Elapsed:F1}s",
            processed, userIds.Count, sw.Elapsed.TotalSeconds);
    }

    private sealed record CachedDishEntry(
        [property: System.Text.Json.Serialization.JsonPropertyName("dishId")] int DishId,
        [property: System.Text.Json.Serialization.JsonPropertyName("score")] float Score);
}
