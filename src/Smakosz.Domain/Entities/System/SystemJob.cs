using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class SystemJob
{
    public int JobId { get; set; }
    public string Type { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Priority { get; set; }
    public string? Payload { get; set; }
    public string? Result { get; set; }
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? WorkerNode { get; set; }
    public int Progress { get; set; }
    public string? ProgressMessage { get; set; }
    public string? ErrorLog { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTime? CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public SystemNode? Worker { get; set; }
}
