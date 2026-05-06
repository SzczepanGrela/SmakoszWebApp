using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public sealed record NcfTrainingSample(int UserId, int DishId, int Rating);

public interface INcfTrainingDatasetBuilder
{
    Task<IReadOnlyList<NcfTrainingSample>> FetchSamplesAsync(int reviewWindowDays, CancellationToken ct);
}

public class NcfTrainingDatasetBuilder : INcfTrainingDatasetBuilder
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;

    public NcfTrainingDatasetBuilder(ISmakoszDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<NcfTrainingSample>> FetchSamplesAsync(int reviewWindowDays, CancellationToken ct)
    {
        var query = _db.Reviews.AsQueryable();

        if (reviewWindowDays > 0)
        {
            var since = _clock.UtcNow.AddDays(-reviewWindowDays);
            query = query.Where(r => r.CreatedAt >= since);
        }

        return await query
            .Where(r => r.IsVisible
                && !r.IsDeleted
                && r.ModerationStatus != ContentModerationStatus.Rejected)
            .Join(_db.Users.Where(u => !u.IsDeleted),
                r => r.UserId, u => u.UserId,
                (r, _) => new NcfTrainingSample(r.UserId, r.DishId, r.DishRating))
            .ToListAsync(ct);
    }
}
