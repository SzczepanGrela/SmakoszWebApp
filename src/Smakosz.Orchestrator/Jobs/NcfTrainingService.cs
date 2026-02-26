using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class NcfTrainingService
{
    private readonly ISmakoszDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDateTimeProvider _clock;
    private readonly NcfTrainingOptions _options;
    private readonly ILogger<NcfTrainingService> _logger;

    public NcfTrainingService(
        ISmakoszDbContext db,
        IFileStorageService storage,
        IHttpClientFactory httpFactory,
        IDateTimeProvider clock,
        IOptions<NcfTrainingOptions> options,
        ILogger<NcfTrainingService> logger)
    {
        _db = db;
        _storage = storage;
        _httpFactory = httpFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ScheduleAsync(CancellationToken ct)
    {
        var since = _clock.UtcNow.AddDays(-_options.ReviewWindowDays);

        var reviews = await _db.Reviews
            .Where(r => r.CreatedAt >= since)
            .Select(r => new { r.UserId, r.DishId, r.DishRating })
            .ToListAsync(ct);

        if (reviews.Count < 100)
        {
            _logger.LogInformation("ncf-training: only {Count} reviews, skipping (min 100)", reviews.Count);
            return;
        }

        // Build CSV
        var csv = new StringBuilder();
        csv.AppendLine("user_id,dish_id,rating");
        foreach (var r in reviews)
            csv.AppendLine($"{r.UserId},{r.DishId},{r.DishRating}");

        var now = _clock.UtcNow;
        var key = $"ncf-training/reviews_{now:yyyyMMdd_HHmmss}.csv";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var csvUrl = await _storage.UploadRawAsync(stream, key, "text/csv", ct);

        // Create SystemJob
        var payload = JsonSerializer.Serialize(new
        {
            csv_url = csvUrl,
            epochs = _options.Epochs,
            batch_size = _options.BatchSize,
            learning_rate = _options.LearningRate,
            embedding_dim = _options.EmbeddingDim,
            review_count = reviews.Count
        });

        _db.SystemJobs.Add(new SystemJob
        {
            Type = "ncf_training",
            Status = JobStatus.Pending,
            Payload = payload,
            CreatedAt = now,
            MaxAttempts = 3
        });

        await _db.SaveChangesAsync(ct);

        // Check GPU health & wake if offline
        var gpuClient = _httpFactory.CreateClient("GpuWorker");
        try
        {
            var health = await gpuClient.GetAsync("/health", ct);
            if (!health.IsSuccessStatusCode)
                await WakeGpuAsync(ct);
        }
        catch
        {
            await WakeGpuAsync(ct);
        }

        _logger.LogInformation(
            "ncf-training: scheduled job with {Reviews} reviews, CSV at {Key}",
            reviews.Count, key);
    }

    private async Task WakeGpuAsync(CancellationToken ct)
    {
        try
        {
            var rpiClient = _httpFactory.CreateClient("RpiGateway");
            await rpiClient.PostAsync("/wake", null, ct);
            _logger.LogInformation("ncf-training: sent WoL request via RPI gateway");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ncf-training: failed to send WoL request");
        }
    }
}
