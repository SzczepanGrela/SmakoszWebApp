using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class ModerationLog
{
    public long LogId { get; set; }
    public ModerationEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public ModerationActor Actor { get; set; }
    public ModerationVerdict Verdict { get; set; }
    public List<string> ReasonCodes { get; set; } = new();
    public string? AdminNote { get; set; }
    public int? ProcessedBy { get; set; }
    public string? AiScores { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? ProcessedByUser { get; set; }
}
