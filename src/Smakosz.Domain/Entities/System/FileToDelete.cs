namespace Smakosz.Domain.Entities.System;

public class FileToDelete
{
    public long FileId { get; set; }
    public string R2Key { get; set; } = string.Empty;
    public string Bucket { get; set; } = "smakosz-photos";
    public string? Reason { get; set; }
    public string? SourceEntity { get; set; }
    public int? SourceId { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
