namespace Smakosz.Application.Features.Worker.DTOs;

public class WorkerConfigDto
{
    public decimal ToxicThresholdApprove { get; set; }
    public decimal ToxicThresholdReject { get; set; }
    public decimal NsfwThresholdApprove { get; set; }
    public decimal NsfwThresholdReject { get; set; }
    public decimal OnTopicThreshold { get; set; }
    public string HerbertModelVersion { get; set; } = "v1";
    public string NsfwModelVersion { get; set; } = "v1";
    public string ClipModelVersion { get; set; } = "v1";
}
