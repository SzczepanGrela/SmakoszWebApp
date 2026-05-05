namespace Smakosz.Domain.Entities.System;

public class AiLog
{
    public long LogId { get; set; }
    public string? ModelType { get; set; }
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? InputSummary { get; set; }
    public string? Scores { get; set; }
    public string? Verdict { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public bool Fallback { get; set; }
    public DateTime CreatedAt { get; set; }
}
