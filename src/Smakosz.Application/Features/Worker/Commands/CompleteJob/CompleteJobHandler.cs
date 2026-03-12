using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Worker.Notifications;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Worker.Commands.CompleteJob;

public class CompleteJobHandler : IRequestHandler<CompleteJobCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IMediator _mediator;

    public CompleteJobHandler(ISmakoszDbContext db, IDateTimeProvider clock, IMediator mediator)
    {
        _db = db;
        _clock = clock;
        _mediator = mediator;
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
            case "text_moderation_batch":
                await HandleTextModerationBatch(request, now, cancellationToken);
                break;
            case "image_moderation":
                await HandleImageModeration(job, request, now, cancellationToken);
                break;
            case "image_moderation_batch":
                await HandleImageModerationBatch(request, now, cancellationToken);
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

        var scores = ParseTextScores(root);
        var toxicityScore = scores.ToxicityScore!.Value;
        var verdict = root.GetProperty("verdict").GetString() ?? "needs_review";
        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;
        var modelName = root.TryGetProperty("model_name", out var mn) ? mn.GetString() : null;

        var moderationEntityType = job.EntityType == "edit_request" ? ModerationEntityType.EditRequest : ModerationEntityType.Review;
        var entityId = int.TryParse(job.EntityId, out var parsedId) ? parsedId : 0;

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
                switch (verdict)
                {
                    case "approved":
                        review.ModerationStatus = ContentModerationStatus.Approved;
                        review.IsApproved = true;
                        break;
                    case "rejected":
                        review.ModerationStatus = ContentModerationStatus.Rejected;
                        review.IsApproved = false;
                        break;
                    default:
                        review.ModerationStatus = ContentModerationStatus.NeedsReview;
                        review.IsApproved = null;
                        break;
                }
            }
        }

        var isAutoApproved = job.EntityType == "edit_request" && verdict == "approved" && toxicityScore < 0.3m;
        await UpsertModerationResultAsync(moderationEntityType, entityId,
            MapStatus(verdict), verdict, modelName, modelVersion,
            scores, isAutoApproved,
            isAutoApproved ? $"AI auto-approved: toxicity={toxicityScore:F3}" : null,
            now, ct);

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = moderationEntityType,
            EntityId = entityId,
            Actor = ModerationActor.Ai,
            Verdict = MapVerdict(verdict),
            AiScores = request.Result
        });

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "text_moderation",
            ModelName = modelName,
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            EntityId = entityId,
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

        editRequest.ModerationStatus = MapStatus(verdict);

        if (verdict == "approved" && toxicityScore < 0.3m)
        {
            editRequest.Status = EditRequestStatus.Approved;
            editRequest.ResolvedAt = now;

            if (editRequest.ChangeScope == EditRequestChangeScope.Dish && editRequest.TargetEntityId.HasValue)
            {
                var dish = await _db.Dishes.FirstOrDefaultAsync(d => d.DishId == editRequest.TargetEntityId.Value, ct);
                if (dish is not null)
                {
                    if (!string.IsNullOrEmpty(editRequest.NewName))
                        dish.DishName = editRequest.NewName;
                    if (!string.IsNullOrEmpty(editRequest.NewDescription))
                        dish.Description = editRequest.NewDescription;
                }
            }
            else if (editRequest.ChangeScope == EditRequestChangeScope.Section && editRequest.TargetEntityId.HasValue)
            {
                var section = await _db.MenuSections.FirstOrDefaultAsync(
                    ms => ms.SectionId == editRequest.TargetEntityId.Value, ct);
                if (section is not null && !string.IsNullOrEmpty(editRequest.NewName))
                    section.SectionName = editRequest.NewName;
            }
            else
            {
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
            }

            var relatedTicket = await _db.SystemTickets
                .FirstOrDefaultAsync(t => t.TicketType == TicketType.EditRequest
                    && t.ReferenceId == editRequest.RequestId
                    && t.Status != TicketStatus.Resolved
                    && t.Status != TicketStatus.Closed, ct);
            if (relatedTicket != null)
                relatedTicket.Status = TicketStatus.Resolved;

            var entityName = editRequest.ChangeScope switch
            {
                EditRequestChangeScope.Dish => "daniu",
                EditRequestChangeScope.Section => "sekcji menu",
                _ => $"restauracji \"{editRequest.Restaurant.RestaurantName}\""
            };

            _db.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = editRequest.UserId,
                Type = NotificationType.System,
                Title = "Edycja zatwierdzona",
                Message = $"Twoje zmiany w {entityName} zostały automatycznie zatwierdzone.",
                CreatedAt = now
            });
        }
    }

    private async Task HandleImageModeration(SystemJob job, CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var root = doc.RootElement;

        var scores = ParseImageScores(root);
        var verdict = root.GetProperty("verdict").GetString() ?? "needs_review";
        var modelVersion = root.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;
        var modelName = root.TryGetProperty("model_name", out var mn) ? mn.GetString() : null;
        var entityId = int.TryParse(job.EntityId, out var parsedId) ? parsedId : 0;

        if (!string.IsNullOrEmpty(job.EntityId) && long.TryParse(job.EntityId, out var assetId))
        {
            var asset = await _db.MediaAssets
                .FirstOrDefaultAsync(a => a.AssetId == assetId, ct);

            if (asset is not null)
            {
                switch (verdict)
                {
                    case "approved":
                        asset.ModerationStatus = ContentModerationStatus.Approved;
                        break;
                    case "rejected":
                        asset.ModerationStatus = ContentModerationStatus.Rejected;
                        break;
                    default:
                        asset.ModerationStatus = ContentModerationStatus.NeedsReview;
                        break;
                }

                if (verdict is "approved" or "rejected")
                {
                    var relatedTicket = await _db.SystemTickets
                        .FirstOrDefaultAsync(t => t.TicketType == TicketType.Photo
                            && t.ReferenceId == assetId
                            && t.Status != TicketStatus.Resolved
                            && t.Status != TicketStatus.Closed, ct);
                    if (relatedTicket != null)
                        relatedTicket.Status = TicketStatus.Resolved;
                }
            }
        }

        await UpsertModerationResultAsync(ModerationEntityType.Photo, entityId,
            MapStatus(verdict), verdict, modelName, modelVersion,
            scores, false, null, now, ct);

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Photo,
            EntityId = entityId,
            Actor = ModerationActor.Ai,
            Verdict = MapVerdict(verdict),
            AiScores = request.Result
        });

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "image_moderation",
            ModelName = modelName,
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            EntityId = entityId,
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

        if (!string.IsNullOrEmpty(modelVersion))
        {
            var versionConfig = await _db.SystemConfigs
                .FirstOrDefaultAsync(c => c.Key == "ncf_model_version", ct);
            if (versionConfig is not null)
                versionConfig.Value = modelVersion;
            else
                _db.SystemConfigs.Add(new SystemConfig
                {
                    Key = "ncf_model_version",
                    Value = modelVersion,
                    Description = "Current NCF model version",
                    UpdatedAt = now
                });
        }

        _db.AiLogs.Add(new AiLog
        {
            ModelType = "ncf_training",
            ModelVersion = modelVersion,
            EntityType = job.EntityType,
            Scores = request.Result,
            Verdict = "completed",
            ProcessingTimeMs = request.ProcessingTimeMs
        });

        if (!string.IsNullOrEmpty(modelVersion))
            await _mediator.Publish(new NcfTrainingCompletedNotification(modelVersion), ct);
    }

    private async Task HandleTextModerationBatch(CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var results = doc.RootElement.GetProperty("results");

        foreach (var item in results.EnumerateArray())
        {
            var entityType = item.GetProperty("entity_type").GetString()!;
            var entityId = item.GetProperty("entity_id").GetInt32();
            var scores = ParseTextScores(item);
            var toxicityScore = scores.ToxicityScore!.Value;
            var verdict = item.GetProperty("verdict").GetString() ?? "needs_review";
            var modelVersion = item.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;
            var modelName = item.TryGetProperty("model_name", out var mn) ? mn.GetString() : null;

            switch (entityType)
            {
                case "review":
                    await ApplyTextModerationToReview(entityId, verdict, ct);
                    break;
                case "edit_request":
                    await ApplyTextModerationToEditRequest(entityId, toxicityScore, verdict, now, ct);
                    break;
                case "dish":
                    await ApplyTextModerationToDish(entityId, verdict, now, ct);
                    break;
                case "restaurant":
                    await ApplyTextModerationToRestaurant(entityId, verdict, ct);
                    break;
                case "menu_section":
                    await ApplyTextModerationToMenuSection(entityId, verdict, ct);
                    break;
            }

            var moderationEntityType = entityType switch
            {
                "review" => ModerationEntityType.Review,
                "edit_request" => ModerationEntityType.EditRequest,
                "dish" => ModerationEntityType.Dish,
                "restaurant" => ModerationEntityType.Restaurant,
                "menu_section" => ModerationEntityType.MenuSection,
                _ => ModerationEntityType.Review
            };

            var isAutoApproved = entityType == "edit_request" && verdict == "approved" && toxicityScore < 0.3m;
            await UpsertModerationResultAsync(moderationEntityType, entityId,
                MapStatus(verdict), verdict, modelName, modelVersion,
                scores, isAutoApproved,
                isAutoApproved ? $"AI auto-approved: toxicity={toxicityScore:F3}" : null,
                now, ct);

            _db.ModerationLogs.Add(new ModerationLog
            {
                EntityType = moderationEntityType,
                EntityId = entityId,
                Actor = ModerationActor.Ai,
                Verdict = MapVerdict(verdict),
                AiScores = item.GetRawText()
            });

            _db.AiLogs.Add(new AiLog
            {
                ModelType = "text_moderation",
                ModelName = modelName,
                ModelVersion = modelVersion,
                EntityType = entityType,
                EntityId = entityId,
                Scores = item.GetRawText(),
                Verdict = verdict,
                ProcessingTimeMs = request.ProcessingTimeMs
            });
        }
    }

    private async Task ApplyTextModerationToReview(int reviewId, string verdict, CancellationToken ct)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);
        if (review is null) return;

        switch (verdict)
        {
            case "approved":
                review.ModerationStatus = ContentModerationStatus.Approved;
                review.IsApproved = true;
                break;
            case "rejected":
                review.ModerationStatus = ContentModerationStatus.Rejected;
                review.IsApproved = false;
                break;
            default:
                review.ModerationStatus = ContentModerationStatus.NeedsReview;
                review.IsApproved = null;
                break;
        }
    }

    private async Task ApplyTextModerationToEditRequest(int requestId, decimal toxicityScore, string verdict, DateTime now, CancellationToken ct)
    {
        var editRequest = await _db.RestaurantEditRequests
            .Include(er => er.Restaurant)
            .FirstOrDefaultAsync(er => er.RequestId == requestId, ct);

        if (editRequest is null) return;

        editRequest.ModerationStatus = MapStatus(verdict);

        if (verdict == "approved" && toxicityScore < 0.3m)
        {
            editRequest.Status = EditRequestStatus.Approved;
            editRequest.ResolvedAt = now;

            if (editRequest.ChangeScope == EditRequestChangeScope.Dish && editRequest.TargetEntityId.HasValue)
            {
                var dish = await _db.Dishes.FirstOrDefaultAsync(d => d.DishId == editRequest.TargetEntityId.Value, ct);
                if (dish is not null)
                {
                    if (!string.IsNullOrEmpty(editRequest.NewName))
                        dish.DishName = editRequest.NewName;
                    if (!string.IsNullOrEmpty(editRequest.NewDescription))
                        dish.Description = editRequest.NewDescription;
                }
            }
            else if (editRequest.ChangeScope == EditRequestChangeScope.Section && editRequest.TargetEntityId.HasValue)
            {
                var section = await _db.MenuSections.FirstOrDefaultAsync(
                    ms => ms.SectionId == editRequest.TargetEntityId.Value, ct);
                if (section is not null && !string.IsNullOrEmpty(editRequest.NewName))
                    section.SectionName = editRequest.NewName;
            }
            else
            {
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
            }

            var relatedTicket = await _db.SystemTickets
                .FirstOrDefaultAsync(t => t.TicketType == TicketType.EditRequest
                    && t.ReferenceId == editRequest.RequestId
                    && t.Status != TicketStatus.Resolved
                    && t.Status != TicketStatus.Closed, ct);
            if (relatedTicket != null)
                relatedTicket.Status = TicketStatus.Resolved;

            var entityName = editRequest.ChangeScope switch
            {
                EditRequestChangeScope.Dish => "daniu",
                EditRequestChangeScope.Section => "sekcji menu",
                _ => $"restauracji \"{editRequest.Restaurant.RestaurantName}\""
            };

            _db.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = editRequest.UserId,
                Type = NotificationType.System,
                Title = "Edycja zatwierdzona",
                Message = $"Twoje zmiany w {entityName} zostały automatycznie zatwierdzone.",
                CreatedAt = now
            });
        }
    }

    private async Task ApplyTextModerationToDish(int dishId, string verdict, DateTime now, CancellationToken ct)
    {
        var dish = await _db.Dishes.FirstOrDefaultAsync(d => d.DishId == dishId, ct);
        if (dish is null) return;

        dish.ModerationStatus = verdict switch
        {
            "approved" => ContentModerationStatus.Approved,
            "rejected" => ContentModerationStatus.Rejected,
            _ => dish.ModerationStatus
        };
    }

    private async Task ApplyTextModerationToRestaurant(int restaurantId, string verdict, CancellationToken ct)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId, ct);
        if (restaurant is null) return;

        restaurant.ModerationStatus = MapStatus(verdict);
    }

    private async Task ApplyTextModerationToMenuSection(int sectionId, string verdict, CancellationToken ct)
    {
        var section = await _db.MenuSections.FirstOrDefaultAsync(ms => ms.SectionId == sectionId, ct);
        if (section is null) return;

        section.ModerationStatus = MapStatus(verdict);
    }

    private async Task HandleImageModerationBatch(CompleteJobCommand request, DateTime now, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Result);
        var results = doc.RootElement.GetProperty("results");

        foreach (var item in results.EnumerateArray())
        {
            var entityType = item.GetProperty("entity_type").GetString()!;
            var entityId = item.GetProperty("entity_id").GetInt64();
            var scores = ParseImageScores(item);
            var verdict = item.GetProperty("verdict").GetString() ?? "needs_review";
            var modelVersion = item.TryGetProperty("model_version", out var mv) ? mv.GetString() : null;
            var modelName = item.TryGetProperty("model_name", out var mn) ? mn.GetString() : null;

            if (entityType == "media_asset")
            {
                var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.AssetId == entityId, ct);
                if (asset is not null)
                {
                    switch (verdict)
                    {
                        case "approved":
                            asset.ModerationStatus = ContentModerationStatus.Approved;
                            break;
                        case "rejected":
                            asset.ModerationStatus = ContentModerationStatus.Rejected;
                            break;
                        default:
                            asset.ModerationStatus = ContentModerationStatus.NeedsReview;
                            break;
                    }

                    if (verdict is "approved" or "rejected")
                    {
                        var relatedTicket = await _db.SystemTickets
                            .FirstOrDefaultAsync(t => t.TicketType == TicketType.Photo
                                && t.ReferenceId == entityId
                                && t.Status != TicketStatus.Resolved
                                && t.Status != TicketStatus.Closed, ct);
                        if (relatedTicket != null)
                            relatedTicket.Status = TicketStatus.Resolved;
                    }
                }
            }

            await UpsertModerationResultAsync(ModerationEntityType.Photo, (int)entityId,
                MapStatus(verdict), verdict, modelName, modelVersion,
                scores, false, null, now, ct);

            _db.ModerationLogs.Add(new ModerationLog
            {
                EntityType = ModerationEntityType.Photo,
                EntityId = (int)entityId,
                Actor = ModerationActor.Ai,
                Verdict = MapVerdict(verdict),
                AiScores = item.GetRawText()
            });

            _db.AiLogs.Add(new AiLog
            {
                ModelType = "image_moderation",
                ModelName = modelName,
                ModelVersion = modelVersion,
                EntityType = entityType,
                EntityId = (int)entityId,
                Scores = item.GetRawText(),
                Verdict = verdict,
                ProcessingTimeMs = request.ProcessingTimeMs
            });
        }
    }

    private async Task UpsertModerationResultAsync(
        ModerationEntityType entityType, int entityId,
        ContentModerationStatus status, string? verdict,
        string? modelName, string? modelVersion, ModerationScores? scores,
        bool autoApproved, string? autoApproveReason,
        DateTime now, CancellationToken ct)
    {
        var serializedScores = scores is not null ? SerializeScores(scores) : null;

        var existing = await _db.ModerationResults
            .FirstOrDefaultAsync(r => r.EntityType == entityType && r.EntityId == entityId, ct);

        if (existing is null)
        {
            _db.ModerationResults.Add(new ModerationResult
            {
                EntityType = entityType,
                EntityId = entityId,
                Status = status,
                AiVerdict = verdict,
                AiModelName = modelName,
                AiModelVersion = modelVersion,
                Scores = serializedScores,
                AutoApproved = autoApproved,
                AutoApproveReason = autoApproveReason,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existing.Status = status;
            existing.AiVerdict = verdict;
            existing.AiModelName = modelName;
            existing.AiModelVersion = modelVersion;
            existing.Scores = serializedScores;
            existing.AutoApproved = autoApproved;
            existing.AutoApproveReason = autoApproveReason;
            existing.ProcessedAt = now;
            existing.UpdatedAt = now;
        }
    }

    private static ModerationScores ParseTextScores(JsonElement root)
    {
        var toxicity = root.GetProperty("toxicity_score").GetDecimal();
        return new ModerationScores(ToxicityScore: toxicity);
    }

    private static ModerationScores ParseImageScores(JsonElement root)
    {
        var nsfw = root.GetProperty("nsfw_score").GetDecimal();
        var relevance = root.GetProperty("relevance_score").GetDecimal();
        return new ModerationScores(NsfwScore: nsfw, RelevanceScore: relevance);
    }

    private static string SerializeScores(ModerationScores scores)
        => JsonSerializer.Serialize(scores, _jsonOptions);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static ModerationVerdict MapVerdict(string verdict) => verdict switch
    {
        "approved" => ModerationVerdict.Approved,
        "rejected" => ModerationVerdict.Rejected,
        _ => ModerationVerdict.NeedsReview
    };

    private static ContentModerationStatus MapStatus(string verdict) => verdict switch
    {
        "approved" => ContentModerationStatus.Approved,
        "rejected" => ContentModerationStatus.Rejected,
        "needs_review" => ContentModerationStatus.NeedsReview,
        _ => ContentModerationStatus.NeedsReview
    };
}
