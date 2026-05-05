namespace Smakosz.Domain.Entities.System;

public class JobProgress
{
    public long ProgressId { get; set; }
    public int JobId { get; set; }
    public int? Epoch { get; set; }
    public double? Loss { get; set; }
    public double? Accuracy { get; set; }
    public double? LearningRate { get; set; }
    public int? CurrentStep { get; set; }
    public int? TotalSteps { get; set; }
    public double? Percentage { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }

    public SystemJob Job { get; set; } = null!;
}
