using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class SystemNode
{
    public string NodeId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? WolGatewayId { get; set; }
    public NodeRole? Role { get; set; }
    public string? Status { get; set; }
    public NodeType NodeType { get; set; } = NodeType.Api;
    public string? Hostname { get; set; }
    public string? GpuName { get; set; }
    public int? GpuMemoryTotal { get; set; }
    public int? GpuMemoryUsed { get; set; }
    public int? CurrentJobId { get; set; }
    public string? Metadata { get; set; }
    public DateTime? LastHeartbeat { get; set; }

    public SystemNode? WolGateway { get; set; }
}
