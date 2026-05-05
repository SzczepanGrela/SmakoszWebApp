using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class ModerationResult
{
    public int ResultId { get; set; }
    public ModerationEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public ContentModerationStatus Status { get; set; }
    public string? AiVerdict { get; set; }
    public string? AiModelName { get; set; }
    public string? AiModelVersion { get; set; }
    public string? Scores { get; set; }
    public string? RejectionReason { get; set; }
    public bool AutoApproved { get; set; }
    public string? AutoApproveReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record ModerationScores(
    decimal? ToxicityScore = null,
    decimal? NsfwScore = null,
    decimal? RelevanceScore = null,
    decimal? Confidence = null);
