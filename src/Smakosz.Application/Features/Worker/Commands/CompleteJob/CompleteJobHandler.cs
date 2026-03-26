using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.CompleteJob;

public class CompleteJobHandler : IRequestHandler<CompleteJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CompleteJobHandler(ISmakoszDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _db.SystemJobs
            .FirstOrDefaultAsync(j => j.JobId == request.JobId, cancellationToken);

        if (job is null)
            return DomainErrors.Job.NotFound;

        if (job.Status != JobStatus.Processing)
            return Error.Conflict("JOB_NOT_PROCESSING", "Job is not in Processing state");

        var now = _clock.UtcNow;

        job.Status = JobStatus.Completed;
        job.Result = request.Result;
        job.FinishedAt = now;
        job.Progress = 100;

        if (!string.IsNullOrEmpty(job.WorkerNode))
        {
            var node = await _db.SystemNodes
                .FirstOrDefaultAsync(n => n.NodeId == job.WorkerNode, cancellationToken);
            if (node is not null)
                node.CurrentJobId = null;
        }

        switch (job.Type)
        {
            case "text_moderation":
                await HandleTextModeration(job, request, now, cancellationToken);
                break;
            case "image_moderation":
                await HandleImageModeration(job, request, now, cancellationToken);
                break;
            case "ncf_training":
                HandleNcfTraining(job, request, now);
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    private async Task HandleTextModeration(SystemJob job, CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var root = doc.RootElement;

        var toxicityScore = root.GetProperty("toxicity_score").GetDecimal();
        var spamScore = root.TryGetProperty("spam_score", out var ss) ? ss.GetDecimal() : (decimal?)null;
        var verdict = root.GetProperty("verdict").GetString() ?? "needs_review";
        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;

        if (!string.IsNullOrEmpty(job.EntityId) && int.TryParse(job.EntityId, out var reviewId))
        {
            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);

            if (review is not null)
            {
                review.AiToxicityScore = toxicityScore;
                review.AiSpamScore = spamScore;
                review.AiVerdict = verdict;
                review.AiModelVersion = modelVersion;
                review.AiProcessedAt = now;

                switch (verdict)
                {
                    case "approved":
                        review.ContentStatus = ReviewContentStatus.Approved;
                        review.IsApproved = true;
                        break;
                    case "rejected":
                        review.ContentStatus = ReviewContentStatus.Rejected;
                        review.IsApproved = false;
                        break;
                    default:
                        review.IsApproved = false;
                        break;
                }
            }
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Review,
            EntityId = int.TryParse(job.EntityId, out var eid) ? eid : 0,
            Actor = ModerationActor.Ai,
            Verdict = MapVerdict(verdict),
            AiScores = request.Result
        });

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "text_moderation",
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            EntityId = int.TryParse(job.EntityId, out var aid) ? aid : 0,
            Scores = request.Result,
            Verdict = verdict,
            ProcessingTimeMs = request.ProcessingTimeMs
        });
    }

    private async Task HandleImageModeration(SystemJob job, CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var root = doc.RootElement;

        var nsfwScore = root.GetProperty("nsfw_score").GetDecimal();
        var onTopicScore = root.GetProperty("on_topic_score").GetDecimal();
        var verdict = root.GetProperty("verdict").GetString() ?? "needs_review";
        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;

        if (!string.IsNullOrEmpty(job.EntityId) && long.TryParse(job.EntityId, out var assetId))
        {
            var asset = await _db.MediaAssets
                .FirstOrDefaultAsync(a => a.AssetId == assetId, ct);

            if (asset is not null)
            {
                asset.AiNsfwScore = nsfwScore;
                asset.AiOnTopicScore = onTopicScore;
                asset.AiVerdict = verdict;
                asset.AiModelVersion = modelVersion;
                asset.AiProcessedAt = now;

                switch (verdict)
                {
                    case "approved":
                        asset.Status = MediaAssetStatus.Approved;
                        break;
                    case "rejected":
                        asset.Status = MediaAssetStatus.Rejected;
                        break;
                }
            }
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Photo,
            EntityId = int.TryParse(job.EntityId, out var eid) ? eid : 0,
            Actor = ModerationActor.Ai,
            Verdict = MapVerdict(verdict),
            AiScores = request.Result
        });

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "image_moderation",
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            EntityId = int.TryParse(job.EntityId, out var aid) ? aid : 0,
            Scores = request.Result,
            Verdict = verdict,
            ProcessingTimeMs = request.ProcessingTimeMs
        });
    }

    private void HandleNcfTraining(SystemJob job, CompleteJobCommand request, DateTime now)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var root = doc.RootElement;

        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "ncf_training",
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            Scores = request.Result,
            Verdict = "completed",
            ProcessingTimeMs = request.ProcessingTimeMs
        });
    }

    private static ModerationVerdict MapVerdict(string verdict) => verdict switch
    {
        "approved" => ModerationVerdict.Approved,
        "rejected" => ModerationVerdict.Rejected,
        _ => ModerationVerdict.NeedsReview
    };
}
