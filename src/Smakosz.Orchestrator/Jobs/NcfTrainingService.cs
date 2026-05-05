using System.Text;
using System.Text.Json;
using ErrorOr;
using Hangfire;
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
    private readonly INcfModelStorageService _modelStorage;
    private readonly IBackgroundJobClient _jobs;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IGpuWakeService _gpuWake;
    private readonly IDateTimeProvider _clock;
    private readonly NcfTrainingOptions _options;
    private readonly IModerationAggregationService _moderationService;
    private readonly ILogger<NcfTrainingService> _logger;

    public NcfTrainingService(
        ISmakoszDbContext db,
        INcfModelStorageService modelStorage,
        IBackgroundJobClient jobs,
        IHttpClientFactory httpFactory,
        IGpuWakeService gpuWake,
        IDateTimeProvider clock,
        IOptions<NcfTrainingOptions> options,
        IModerationAggregationService moderationService,
        ILogger<NcfTrainingService> logger)
    {
        _db = db;
        _modelStorage = modelStorage;
        _jobs = jobs;
        _httpFactory = httpFactory;
        _gpuWake = gpuWake;
        _clock = clock;
        _options = options.Value;
        _moderationService = moderationService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct)
    {
        var query = _db.Reviews.AsQueryable();

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

        var csv = new StringBuilder();
        csv.AppendLine("user_id,dish_id,rating");
        foreach (var r in reviews)
            csv.AppendLine($"{r.UserId},{r.DishId},{r.DishRating}");

        var now = _clock.UtcNow;
        var key = $"ncf-training/reviews_{now:yyyyMMdd_HHmmss}.csv";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var csvUrl = await _modelStorage.UploadTrainingDataAsync(stream, key, ct);

        var payload = JsonSerializer.Serialize(new
        {
            csv_url = csvUrl,
            epochs = _options.Epochs,
            batch_size = _options.BatchSize,
            learning_rate = _options.LearningRate,
            embedding_dim = _options.EmbeddingDim,
            review_count = reviews.Count
        });

        var job = await _db.SystemJobs
            .Where(j => j.Type == "ncf_training" && j.Status == JobStatus.Pending)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            _logger.LogWarning("ncf-training: no pre-inserted pending row found, creating fallback");
            job = new SystemJob
            {
                Type = "ncf_training",
                Status = JobStatus.Pending,
                Payload = payload,
                CreatedAt = now,
                MaxAttempts = 3
            };
            _db.SystemJobs.Add(job);
        }
        else
        {
            job.Payload = payload;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ncf-training: aggregating pending moderations before GPU wake-up");
        var modConfigs = await _db.SystemConfigs
            .Where(c => c.Key == "moderation.text_batch_size" || c.Key == "moderation.image_batch_size")
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
        var textBatchSize = modConfigs.TryGetValue("moderation.text_batch_size", out var tv) && int.TryParse(tv, out var tbs) ? tbs : 100;
        var imageBatchSize = modConfigs.TryGetValue("moderation.image_batch_size", out var iv) && int.TryParse(iv, out var ibs) ? ibs : 10;
        await _moderationService.AggregateAsync(textBatchSize, imageBatchSize, ct);

        var gpuClient = _httpFactory.CreateClient("GpuWorker");
        try
        {
            var health = await gpuClient.GetAsync("/health", ct);
            if (!health.IsSuccessStatusCode)
                await _gpuWake.WakeAsync(ct);
        }
        catch
        {
            await _gpuWake.WakeAsync(ct);
        }

        _jobs.Enqueue<INcfModelStorageService>(x => x.CleanupOldFilesAsync("ncf-training/", 2, CancellationToken.None));

        _logger.LogInformation(
            "ncf-training: scheduled job with {Reviews} reviews, CSV at {Key}",
            reviews.Count, key);

        return Result.Success;
    }

}
