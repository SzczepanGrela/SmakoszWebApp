using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetNcfStatus;

public record GetNcfStatusQuery() : IRequest<ErrorOr<NcfStatusDto>>;

public class GetNcfStatusHandler : IRequestHandler<GetNcfStatusQuery, ErrorOr<NcfStatusDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IRecommendationProvider _provider;
    private readonly ICurrentUserService _currentUser;

    public GetNcfStatusHandler(
        ISmakoszDbContext db,
        IRecommendationProvider provider,
        ICurrentUserService currentUser)
    {
        _db = db;
        _provider = provider;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<NcfStatusDto>> Handle(GetNcfStatusQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var loadedVersion = _provider.GetLoadedVersion();
        var mappedUsersCount = _provider.GetMappedUserIds().Count;

        var cacheForCurrent = string.IsNullOrEmpty(loadedVersion)
            ? 0
            : await _db.UserRecommendationCaches
                .AsNoTracking()
                .Where(c => c.ModelVersion == loadedVersion)
                .CountAsync(cancellationToken);

        var cachePercent = mappedUsersCount > 0
            ? cacheForCurrent / (double)mappedUsersCount * 100.0
            : 0.0;

        var recent = await _db.SystemJobs.AsNoTracking()
            .Where(j => j.Type == "ncf_training")
            .OrderByDescending(j => j.CreatedAt)
            .Take(5)
            .Select(j => new NcfTrainingSummaryDto
            {
                JobId = j.JobId,
                Status = j.Status.ToString(),
                CreatedAt = j.CreatedAt,
                FinishedAt = j.FinishedAt,
                DurationSeconds = j.StartedAt.HasValue && j.FinishedAt.HasValue
                    ? (j.FinishedAt.Value - j.StartedAt.Value).TotalSeconds
                    : (double?)null,
                WorkerNode = j.WorkerNode,
                ErrorMessage = j.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        NcfRegenSummaryDto? regen = null;
        if (!string.IsNullOrEmpty(loadedVersion) && cacheForCurrent > 0)
        {
            var batch = await _db.UserRecommendationCaches
                .AsNoTracking()
                .Where(c => c.ModelVersion == loadedVersion)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    First = g.Min(c => c.GeneratedAt),
                    Last = g.Max(c => c.GeneratedAt)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (batch is not null)
            {
                regen = new NcfRegenSummaryDto
                {
                    FirstRowInBatch = batch.First,
                    LastRow = batch.Last
                };
            }
        }

        return new NcfStatusDto
        {
            NcfAvailable = _provider.IsAvailable,
            FallbackReason = _provider.FallbackReason,
            LoadedVersion = loadedVersion,
            MappedUsersCount = mappedUsersCount,
            CachePopulatedCount = cacheForCurrent,
            CachePopulatedPercent = cachePercent,
            LastTraining = recent.FirstOrDefault(),
            LastCacheRegen = regen,
            RecentTrainings = recent
        };
    }
}
