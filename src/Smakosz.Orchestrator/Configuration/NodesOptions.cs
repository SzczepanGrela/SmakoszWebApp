namespace Smakosz.Orchestrator.Configuration;

public class NodesOptions
{
    public const string SectionName = "Nodes";

    public NodeIdentityConfig Api { get; set; } = new();
    public NodeIdentityConfig RbpiGateway { get; set; } = new();
    public NodeIdentityConfig GpuWorker { get; set; } = new();
}

public class NodeIdentityConfig
{
    public string NodeId { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? WolGatewayId { get; set; }
}
