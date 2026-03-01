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
                await HandleNcfTraining(job, request, now, cancellationToken);
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

        if (job.EntityType == "edit_request")
        {
            await HandleEditRequestModeration(job, toxicityScore, verdict, modelVersion, now, ct);
        }
        else if (!string.IsNullOrEmpty(job.EntityId) && int.TryParse(job.EntityId, out var reviewId))
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
            EntityType = job.EntityType == "edit_request" ? ModerationEntityType.EditRequest : ModerationEntityType.Review,
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

    private async Task HandleEditRequestModeration(SystemJob job, decimal toxicityScore, string verdict, string? modelVersion, DateTime now, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.EntityId) || !int.TryParse(job.EntityId, out var requestId))
            return;

        var editRequest = await _db.RestaurantEditRequests
            .Include(er => er.Restaurant)
            .FirstOrDefaultAsync(er => er.RequestId == requestId, ct);

        if (editRequest is null)
            return;

        editRequest.AiVerdict = verdict;
        editRequest.AiConfidence = 1.0m - toxicityScore;
        editRequest.AiModelVersion = modelVersion;
        editRequest.AiProcessedAt = now;

        if (verdict == "approved" && toxicityScore < 0.3m)
        {
            editRequest.AutoApproved = true;
            editRequest.AutoApproveReason = $"AI auto-approved: toxicity={toxicityScore:F3}";
            editRequest.Status = EditRequestStatus.Approved;
            editRequest.ResolvedAt = now;

            if (!string.IsNullOrEmpty(editRequest.NewName))
                editRequest.Restaurant.RestaurantName = editRequest.NewName;
            if (!string.IsNullOrEmpty(editRequest.NewDescription))
                editRequest.Restaurant.Description = editRequest.NewDescription;
            if (!string.IsNullOrEmpty(editRequest.NewAddress))
                editRequest.Restaurant.Address = editRequest.NewAddress;
            if (!string.IsNullOrEmpty(editRequest.NewPhone))
                editRequest.Restaurant.Phone = editRequest.NewPhone;
            if (!string.IsNullOrEmpty(editRequest.NewWebsite))
                editRequest.Restaurant.Website = editRequest.NewWebsite;

            var relatedTicket = await _db.SystemTickets
                .FirstOrDefaultAsync(t => t.TicketType == TicketType.EditRequest
                    && t.ReferenceId == editRequest.RequestId
                    && t.Status != TicketStatus.Resolved
                    && t.Status != TicketStatus.Closed, ct);
            if (relatedTicket != null)
                relatedTicket.Status = TicketStatus.Resolved;

            _db.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = editRequest.UserId,
                Type = NotificationType.System,
                Title = "Edycja zatwierdzona",
                Message = $"Twoje zmiany w restauracji \"{editRequest.Restaurant.RestaurantName}\" zostaly automatycznie zatwierdzone.",
                CreatedAt = now
            });
        }
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

                // Close related ticket
                var relatedTicket = await _db.SystemTickets
                    .FirstOrDefaultAsync(t => t.TicketType == TicketType.Photo
                        && t.ReferenceId == assetId
                        && t.Status != TicketStatus.Resolved
                        && t.Status != TicketStatus.Closed, ct);
                if (relatedTicket != null)
                    relatedTicket.Status = TicketStatus.Resolved;
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

    private async Task HandleNcfTraining(SystemJob job, CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var root = doc.RootElement;

        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;

        // Update system config with new NCF model version
        if (!string.IsNullOrEmpty(modelVersion))
        {
            var versionConfig = await _db.SystemConfigs
                .FirstOrDefaultAsync(c => c.Key == "ncf_model_version", ct);
            if (versionConfig is not null)
                versionConfig.Value = modelVersion;
            else
                _db.SystemConfigs.Add(new Domain.Entities.System.SystemConfig
                {
                    Key = "ncf_model_version",
                    Value = modelVersion,
                    Description = "Current NCF model version",
                    UpdatedAt = now
                });
        }

        var availableConfig = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == "ncf_available", ct);
        if (availableConfig is not null)
            availableConfig.Value = "true";
        else
            _db.SystemConfigs.Add(new Domain.Entities.System.SystemConfig
            {
                Key = "ncf_available",
                Value = "true",
                Description = "Whether NCF recommendations are available",
                UpdatedAt = now
            });

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
