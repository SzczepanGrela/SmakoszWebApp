using System.Text.Json;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class ModerationBatchAggregatorService : IModerationAggregationService
{
    private readonly ISmakoszDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IGpuWakeService _gpuWake;
    private readonly IDateTimeProvider _clock;
    private readonly GpuWorkerOptions _gpuOptions;
    private readonly ILogger<ModerationBatchAggregatorService> _logger;

    public ModerationBatchAggregatorService(
        ISmakoszDbContext db,
        IHttpClientFactory httpFactory,
        IGpuWakeService gpuWake,
        IDateTimeProvider clock,
        IOptions<GpuWorkerOptions> gpuOptions,
        ILogger<ModerationBatchAggregatorService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _gpuWake = gpuWake;
        _clock = clock;
        _gpuOptions = gpuOptions.Value;
        _logger = logger;
    }

    public async Task AggregateAsync(int textBatchSize, int imageBatchSize, CancellationToken ct)
    {
        IDisposable? distributedLock = null;
        try
        {
            var connection = JobStorage.Current.GetConnection();
            distributedLock = connection.AcquireDistributedLock("moderation-aggregation", TimeSpan.FromSeconds(10));
        }
        catch (DistributedLockTimeoutException)
        {
            _logger.LogInformation("Moderation aggregation skipped - another instance is running");
            return;
        }

        try
        {
            await AggregateTextBatchesAsync(textBatchSize, ct);
            await AggregateImageBatchesAsync(imageBatchSize, ct);
            await _gpuWake.WakeAsync(ct);
        }
        finally
        {
            distributedLock?.Dispose();
        }
    }

    private async Task AggregateTextBatchesAsync(int batchSize, CancellationToken ct)
    {
        while (true)
        {
            var items = new List<BatchItem>();

            var pendingReviews = await _db.Reviews
                .Where(r => r.ModerationStatus == ContentModerationStatus.Pending && !r.IsDeleted && r.Content != null)
                .OrderBy(r => r.CreatedAt)
                .Take(batchSize)
                .Select(r => new { r.ReviewId, r.Content })
                .ToListAsync(ct);

            foreach (var r in pendingReviews)
            {
                items.Add(new BatchItem("review", r.ReviewId, r.Content!));
                if (items.Count >= batchSize) break;
            }

            if (items.Count < batchSize)
            {
                var pendingEdits = await _db.RestaurantEditRequests
                    .Where(er => er.ModerationStatus == ContentModerationStatus.Pending
                        && er.Status == EditRequestStatus.Pending)
                    .OrderBy(er => er.CreatedAt)
                    .Take(batchSize - items.Count)
                    .Select(er => new { er.RequestId, er.NewName, er.NewDescription })
                    .ToListAsync(ct);

                foreach (var er in pendingEdits)
                {
                    var text = string.Join("\n\n", new[] { er.NewName, er.NewDescription }.Where(t => !string.IsNullOrEmpty(t)));
                    items.Add(new BatchItem("edit_request", er.RequestId, text));
                    if (items.Count >= batchSize) break;
                }
            }

            if (items.Count < batchSize)
            {
                var pendingDishes = await _db.Dishes
                    .Where(d => d.ModerationStatus == ContentModerationStatus.Pending)
                    .OrderBy(d => d.CreatedAt)
                    .Take(batchSize - items.Count)
                    .Select(d => new { d.DishId, d.DishName, d.Description })
                    .ToListAsync(ct);

                foreach (var d in pendingDishes)
                {
                    var text = string.IsNullOrEmpty(d.Description)
                        ? d.DishName
                        : $"{d.DishName}\n\n{d.Description}";
                    items.Add(new BatchItem("dish", d.DishId, text));
                    if (items.Count >= batchSize) break;
                }
            }

            if (items.Count < batchSize)
            {
                var pendingRestaurants = await _db.Restaurants
                    .Where(r => r.ModerationStatus == ContentModerationStatus.Pending)
                    .OrderBy(r => r.CreatedAt)
                    .Take(batchSize - items.Count)
                    .Select(r => new { r.RestaurantId, r.RestaurantName, r.Description })
                    .ToListAsync(ct);

                foreach (var r in pendingRestaurants)
                {
                    var text = string.IsNullOrEmpty(r.Description)
                        ? r.RestaurantName
                        : $"{r.RestaurantName}\n\n{r.Description}";
                    items.Add(new BatchItem("restaurant", r.RestaurantId, text));
                    if (items.Count >= batchSize) break;
                }
            }

            if (items.Count < batchSize)
            {
                var pendingSections = await _db.MenuSections
                    .Where(ms => ms.ModerationStatus == ContentModerationStatus.Pending)
                    .OrderBy(ms => ms.CreatedAt)
                    .Take(batchSize - items.Count)
                    .Select(ms => new { ms.SectionId, ms.SectionName })
                    .ToListAsync(ct);

                foreach (var ms in pendingSections)
                {
                    items.Add(new BatchItem("menu_section", ms.SectionId, ms.SectionName));
                    if (items.Count >= batchSize) break;
                }
            }

            if (items.Count == 0)
                break;

            var payloadItems = items.Select(item => new Dictionary<string, object>
            {
                ["entity_type"] = item.EntityType,
                ["entity_id"] = item.EntityId,
                ["text"] = item.Text,
                ["language"] = "pl"
            }).ToList<object>();

            _db.SystemJobs.Add(new SystemJob
            {
                Type = "text_moderation_batch",
                Status = JobStatus.Pending,
                Priority = 5,
                Payload = JsonSerializer.Serialize(new { items = payloadItems })
            });

            await MarkAsProcessingAsync(items, ct);

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created text_moderation_batch with {Count} items", items.Count);

            if (items.Count < batchSize)
                break;
        }
    }

    private async Task AggregateImageBatchesAsync(int batchSize, CancellationToken ct)
    {
        while (true)
        {
            var pendingAssets = await _db.MediaAssets
                .Where(a => a.ModerationStatus == ContentModerationStatus.Pending)
                .OrderBy(a => a.CreatedAt)
                .Take(batchSize)
                .Select(a => new { a.AssetId, a.Url })
                .ToListAsync(ct);

            if (pendingAssets.Count == 0)
                break;

            var payloadItems = pendingAssets.Select(a => new
            {
                entity_type = "media_asset",
                entity_id = a.AssetId,
                image_url = a.Url
            }).ToList();

            _db.SystemJobs.Add(new SystemJob
            {
                Type = "image_moderation_batch",
                Status = JobStatus.Pending,
                Priority = 5,
                Payload = JsonSerializer.Serialize(new { items = payloadItems })
            });

            var assetIds = pendingAssets.Select(a => a.AssetId).ToList();
            var assets = await _db.MediaAssets
                .Where(a => assetIds.Contains(a.AssetId))
                .ToListAsync(ct);
            foreach (var asset in assets)
                asset.ModerationStatus = ContentModerationStatus.Processing;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created image_moderation_batch with {Count} items", pendingAssets.Count);

            if (pendingAssets.Count < batchSize)
                break;
        }
    }

    private async Task MarkAsProcessingAsync(List<BatchItem> items, CancellationToken ct)
    {
        var reviewIds = items.Where(i => i.EntityType == "review").Select(i => i.EntityId).ToList();
        var editRequestIds = items.Where(i => i.EntityType == "edit_request").Select(i => i.EntityId).ToList();
        var dishIds = items.Where(i => i.EntityType == "dish").Select(i => i.EntityId).ToList();
        var restaurantIds = items.Where(i => i.EntityType == "restaurant").Select(i => i.EntityId).ToList();

        if (reviewIds.Count > 0)
        {
            var reviews = await _db.Reviews.Where(r => reviewIds.Contains(r.ReviewId)).ToListAsync(ct);
            foreach (var r in reviews) r.ModerationStatus = ContentModerationStatus.Processing;
        }

        if (editRequestIds.Count > 0)
        {
            var edits = await _db.RestaurantEditRequests.Where(er => editRequestIds.Contains(er.RequestId)).ToListAsync(ct);
            foreach (var er in edits) er.ModerationStatus = ContentModerationStatus.Processing;
        }

        if (dishIds.Count > 0)
        {
            var dishes = await _db.Dishes.Where(d => dishIds.Contains(d.DishId)).ToListAsync(ct);
            foreach (var d in dishes) d.ModerationStatus = ContentModerationStatus.Processing;
        }

        if (restaurantIds.Count > 0)
        {
            var restaurants = await _db.Restaurants.Where(r => restaurantIds.Contains(r.RestaurantId)).ToListAsync(ct);
            foreach (var r in restaurants) r.ModerationStatus = ContentModerationStatus.Processing;
        }

        var menuSectionIds = items.Where(i => i.EntityType == "menu_section").Select(i => i.EntityId).ToList();
        if (menuSectionIds.Count > 0)
        {
            var sections = await _db.MenuSections.Where(ms => menuSectionIds.Contains(ms.SectionId)).ToListAsync(ct);
            foreach (var ms in sections) ms.ModerationStatus = ContentModerationStatus.Processing;
        }
    }

    private record BatchItem(string EntityType, int EntityId, string Text);
}
