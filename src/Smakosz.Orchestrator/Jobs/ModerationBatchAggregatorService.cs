using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class ModerationBatchAggregatorService : IModerationAggregationService
{
    private readonly ISmakoszDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDateTimeProvider _clock;
    private readonly GpuWorkerOptions _gpuOptions;
    private readonly ILogger<ModerationBatchAggregatorService> _logger;

    private const int BatchSize = 100;

    public ModerationBatchAggregatorService(
        ISmakoszDbContext db,
        IHttpClientFactory httpFactory,
        IDateTimeProvider clock,
        IOptions<GpuWorkerOptions> gpuOptions,
        ILogger<ModerationBatchAggregatorService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _clock = clock;
        _gpuOptions = gpuOptions.Value;
        _logger = logger;
    }

    public async Task AggregateAsync(CancellationToken ct)
    {
        await AggregateTextBatchesAsync(ct);
        await AggregateImageBatchesAsync(ct);
        await WakeGpuIfNeededAsync(ct);
    }

    private async Task AggregateTextBatchesAsync(CancellationToken ct)
    {
        while (true)
        {
            var items = new List<BatchItem>();

            // Reviews with pending text moderation
            var pendingReviews = await _db.Reviews
                .Where(r => r.ModerationStatus == ContentModerationStatus.Pending && !r.IsDeleted && r.Content != null)
                .OrderBy(r => r.CreatedAt)
                .Take(BatchSize)
                .Select(r => new { r.ReviewId, r.Content })
                .ToListAsync(ct);

            foreach (var r in pendingReviews)
            {
                items.Add(new BatchItem("review", r.ReviewId, r.Content!));
                if (items.Count >= BatchSize) break;
            }

            // Edit requests with pending moderation
            if (items.Count < BatchSize)
            {
                var pendingEdits = await _db.RestaurantEditRequests
                    .Where(er => er.ModerationStatus == ContentModerationStatus.Pending
                        && er.Status == EditRequestStatus.Pending)
                    .OrderBy(er => er.CreatedAt)
                    .Take(BatchSize - items.Count)
                    .Select(er => new { er.RequestId, er.NewName, er.NewDescription })
                    .ToListAsync(ct);

                foreach (var er in pendingEdits)
                {
                    var text = string.Join("\n\n", new[] { er.NewName, er.NewDescription }.Where(t => !string.IsNullOrEmpty(t)));
                    items.Add(new BatchItem("edit_request", er.RequestId, text));
                    if (items.Count >= BatchSize) break;
                }
            }

            // Dishes with pending moderation (from CreateDish; UpdateDish goes through EditRequest)
            if (items.Count < BatchSize)
            {
                var pendingDishes = await _db.Dishes
                    .Where(d => d.ModerationStatus == ContentModerationStatus.Pending)
                    .OrderBy(d => d.CreatedAt)
                    .Take(BatchSize - items.Count)
                    .Select(d => new { d.DishId, d.DishName, d.Description })
                    .ToListAsync(ct);

                foreach (var d in pendingDishes)
                {
                    var text = string.IsNullOrEmpty(d.Description)
                        ? d.DishName
                        : $"{d.DishName}\n\n{d.Description}";
                    items.Add(new BatchItem("dish", d.DishId, text));
                    if (items.Count >= BatchSize) break;
                }
            }

            // Restaurants with pending moderation
            if (items.Count < BatchSize)
            {
                var pendingRestaurants = await _db.Restaurants
                    .Where(r => r.ModerationStatus == ContentModerationStatus.Pending)
                    .OrderBy(r => r.CreatedAt)
                    .Take(BatchSize - items.Count)
                    .Select(r => new { r.RestaurantId, r.RestaurantName, r.Description })
                    .ToListAsync(ct);

                foreach (var r in pendingRestaurants)
                {
                    var text = string.IsNullOrEmpty(r.Description)
                        ? r.RestaurantName
                        : $"{r.RestaurantName}\n\n{r.Description}";
                    items.Add(new BatchItem("restaurant", r.RestaurantId, text));
                    if (items.Count >= BatchSize) break;
                }
            }

            if (items.Count == 0)
                break;

            // Build payload
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

            // Mark entities as Processing
            await MarkAsProcessingAsync(items, ct);

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created text_moderation_batch with {Count} items", items.Count);

            if (items.Count < BatchSize)
                break;
        }
    }

    private async Task AggregateImageBatchesAsync(CancellationToken ct)
    {
        while (true)
        {
            var pendingAssets = await _db.MediaAssets
                .Where(a => a.ModerationStatus == ContentModerationStatus.Pending)
                .OrderBy(a => a.CreatedAt)
                .Take(BatchSize)
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

            // Mark as Processing
            var assetIds = pendingAssets.Select(a => a.AssetId).ToList();
            var assets = await _db.MediaAssets
                .Where(a => assetIds.Contains(a.AssetId))
                .ToListAsync(ct);
            foreach (var asset in assets)
                asset.ModerationStatus = ContentModerationStatus.Processing;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created image_moderation_batch with {Count} items", pendingAssets.Count);

            if (pendingAssets.Count < BatchSize)
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
    }

    private async Task WakeGpuIfNeededAsync(CancellationToken ct)
    {
        try
        {
            var gpuNode = await _db.SystemNodes
                .FirstOrDefaultAsync(n => n.NodeType == NodeType.Gpu, ct);

            if (gpuNode is null || gpuNode.Status == "online")
                return;

            var rpiClient = _httpFactory.CreateClient("RpiGateway");
            await rpiClient.PostAsync("/wake", null, ct);
            _logger.LogInformation("Sent WoL to GPU worker via RPI gateway");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to wake GPU worker");
        }
    }

    private record BatchItem(string EntityType, int EntityId, string Text);
}
