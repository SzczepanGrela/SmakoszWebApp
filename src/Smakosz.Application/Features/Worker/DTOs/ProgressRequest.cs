namespace Smakosz.Application.Features.Worker.DTOs;

public class ProgressRequest
{
    public int? Epoch { get; set; }
    public double? Loss { get; set; }
    public double? Accuracy { get; set; }
    public double? LearningRate { get; set; }
    public int? CurrentStep { get; set; }
    public int? TotalSteps { get; set; }
    public string? Message { get; set; }
}
