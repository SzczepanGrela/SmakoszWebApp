using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class ModerationAggregationSchedulerService
{
    private readonly IModerationAggregationService _aggregator;
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ModerationAggregationSchedulerService> _logger;

    public ModerationAggregationSchedulerService(
        IModerationAggregationService aggregator,
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<ModerationAggregationSchedulerService> logger)
    {
        _aggregator = aggregator;
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var enabled = await GetConfigBoolAsync("moderation.auto_enabled", true, ct);
        if (!enabled)
            return;

        var intervalMinutes = await GetConfigIntAsync("moderation.auto_interval_minutes", 5, ct);
        var lastRun = await GetConfigDateTimeAsync("moderation.last_aggregation_utc", ct);
        var intervalElapsed = !lastRun.HasValue
            || (_clock.UtcNow - lastRun.Value).TotalMinutes >= intervalMinutes;

        var textBatchSize = await GetConfigIntAsync("moderation.text_batch_size", 100, ct);
        var imageBatchSize = await GetConfigIntAsync("moderation.image_batch_size", 10, ct);
        var pendingTextCount = await CountPendingTextAsync(ct);
        var pendingImageCount = await _db.MediaAssets
            .CountAsync(a => a.ModerationStatus == ContentModerationStatus.Pending, ct);
        var thresholdReached = pendingTextCount >= textBatchSize || pendingImageCount >= imageBatchSize;

        if (!intervalElapsed && !thresholdReached)
            return;

        if (pendingTextCount == 0 && pendingImageCount == 0)
            return;

        _logger.LogInformation(
            "Moderation auto-aggregation triggered (interval={IntervalElapsed}, threshold={ThresholdReached}, text={TextCount}, image={ImageCount})",
            intervalElapsed, thresholdReached, pendingTextCount, pendingImageCount);

        await _aggregator.AggregateAsync(ct);

        await UpsertConfigAsync("moderation.last_aggregation_utc", _clock.UtcNow.ToString("O"), ct);
    }

    private async Task<int> CountPendingTextAsync(CancellationToken ct)
    {
        var reviews = await _db.Reviews
            .CountAsync(r => r.ModerationStatus == ContentModerationStatus.Pending && !r.IsDeleted && r.Content != null, ct);
        var edits = await _db.RestaurantEditRequests
            .CountAsync(e => e.ModerationStatus == ContentModerationStatus.Pending && e.Status == EditRequestStatus.Pending, ct);
        var dishes = await _db.Dishes
            .CountAsync(d => d.ModerationStatus == ContentModerationStatus.Pending, ct);
        var restaurants = await _db.Restaurants
            .CountAsync(r => r.ModerationStatus == ContentModerationStatus.Pending, ct);
        var sections = await _db.MenuSections
            .CountAsync(ms => ms.ModerationStatus == ContentModerationStatus.Pending, ct);

        return reviews + edits + dishes + restaurants + sections;
    }

    private async Task<bool> GetConfigBoolAsync(string key, bool defaultValue, CancellationToken ct)
    {
        var value = await _db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        return value is not null ? bool.TryParse(value, out var b) && b : defaultValue;
    }

    private async Task<int> GetConfigIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var value = await _db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        return value is not null && int.TryParse(value, out var i) ? i : defaultValue;
    }

    private async Task<DateTime?> GetConfigDateTimeAsync(string key, CancellationToken ct)
    {
        var value = await _db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        return value is not null && DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : null;
    }

    private async Task UpsertConfigAsync(string key, string value, CancellationToken ct)
    {
        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);
        if (config is not null)
        {
            config.Value = value;
        }
        else
        {
            _db.SystemConfigs.Add(new Domain.Entities.System.SystemConfig
            {
                Key = key,
                Value = value,
                Description = "Timestamp ostatniej automatycznej agregacji moderacji",
                IsSecret = false,
                IsPublic = false
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
