using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class NcfTrainingService : INcfTrainingService
{
    private readonly ISmakoszDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NcfTrainingService> _logger;

    public NcfTrainingService(
        ISmakoszDbContext db,
        IFileStorageService storage,
        IDateTimeProvider clock,
        ILogger<NcfTrainingService> logger)
    {
        _db = db;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct)
    {
        var since = _clock.UtcNow.AddDays(-90);

        var reviews = await _db.Reviews
            .Where(r => r.CreatedAt >= since
                && r.IsVisible
                && !r.IsDeleted
                && r.ContentStatus != ReviewContentStatus.Rejected)
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
        var csvUrl = await _storage.UploadRawAsync(stream, key, "text/csv", ct);

        var payload = JsonSerializer.Serialize(new
        {
            csv_url = csvUrl,
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

        _logger.LogInformation(
            "ncf-training: scheduled job with {Reviews} reviews, CSV at {Key}",
            reviews.Count, key);

        return Result.Success;
    }
}
