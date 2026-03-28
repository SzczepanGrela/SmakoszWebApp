using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class NcfTrainingService : INcfTrainingService
{
    private readonly ISmakoszDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDateTimeProvider _clock;
    private readonly NcfTrainingOptions _options;
    private readonly IModerationAggregationService _moderationService;
    private readonly ILogger<NcfTrainingService> _logger;

    public NcfTrainingService(
        ISmakoszDbContext db,
        IFileStorageService storage,
        IHttpClientFactory httpFactory,
        IDateTimeProvider clock,
        IOptions<NcfTrainingOptions> options,
        IModerationAggregationService moderationService,
        ILogger<NcfTrainingService> logger)
    {
        _db = db;
        _storage = storage;
        _httpFactory = httpFactory;
        _clock = clock;
        _options = options.Value;
        _moderationService = moderationService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct)
    {
        var query = _db.Reviews.AsQueryable();

        // ReviewWindowDays=0 means all reviews (no time filter)
        if (_options.ReviewWindowDays > 0)
        {
            var since = _clock.UtcNow.AddDays(-_options.ReviewWindowDays);
            query = query.Where(r => r.CreatedAt >= since);
        }

        var reviews = await query
            .Where(r => r.IsVisible
                && !r.IsDeleted
                && r.ModerationStatus != ContentModerationStatus.Rejected)
            .Join(_db.Users.Where(u => !u.IsDeleted),
                r => r.UserId, u => u.UserId,
                (r, u) => new { r.UserId, r.DishId, r.DishRating })
            .ToListAsync(ct);

        if (reviews.Count < 100)
        {
            _logger.LogInformation("ncf-training: only {Count} reviews, skipping (min 100)", reviews.Count);
            return Error.Validation("NCF_INSUFFICIENT_REVIEWS",
                $"Za mało recenzji do treningu NCF: {reviews.Count} (wymagane min. 100)");
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

        // Aggregate pending moderations before GPU wake-up
        _logger.LogInformation("ncf-training: aggregating pending moderations before GPU wake-up");
        await _moderationService.AggregateAsync(ct);

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

        return Result.Success;
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
