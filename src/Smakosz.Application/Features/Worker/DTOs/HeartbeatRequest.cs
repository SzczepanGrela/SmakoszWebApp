namespace Smakosz.Application.Features.Worker.DTOs;

public class HeartbeatRequest
{
    public string NodeId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? GpuName { get; set; }
    public int? GpuMemoryTotal { get; set; }
    public int? GpuMemoryUsed { get; set; }
    public int? CurrentJobId { get; set; }
    public string? Metadata { get; set; }
}
