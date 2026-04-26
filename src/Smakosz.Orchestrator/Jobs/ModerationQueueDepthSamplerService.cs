using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.Orchestrator.Jobs;

public class ModerationQueueDepthSamplerService
{
    private readonly SmakoszDbContext _db;
    private readonly IBusinessMetrics _metrics;

    public ModerationQueueDepthSamplerService(SmakoszDbContext db, IBusinessMetrics metrics)
    {
        _db = db;
        _metrics = metrics;
    }

    public async Task SampleAsync(CancellationToken cancellationToken)
    {
        var pendingReviews = await _db.Reviews
            .CountAsync(r => r.ModerationStatus == ContentModerationStatus.Pending, cancellationToken);

        var pendingPhotos = await _db.MediaAssets
            .CountAsync(m => m.ModerationStatus == ContentModerationStatus.Pending, cancellationToken);

        var pendingReports = await _db.Reports
            .CountAsync(r => r.Status == ReportStatus.Pending, cancellationToken);

        _metrics.SetModerationQueueDepth("review", pendingReviews);
        _metrics.SetModerationQueueDepth("photo", pendingPhotos);
        _metrics.SetModerationQueueDepth("report", pendingReports);
    }
}
