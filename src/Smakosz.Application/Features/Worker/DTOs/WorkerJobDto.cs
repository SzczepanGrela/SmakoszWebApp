namespace Smakosz.Application.Features.Worker.DTOs;

public class WorkerJobDto
{
    public int JobId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
    public int MaxAttempts { get; set; }
    public int Priority { get; set; }
}
