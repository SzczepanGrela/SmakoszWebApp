namespace Smakosz.Orchestrator.Configuration;

public class RpiGatewayOptions
{
    public const string SectionName = "RpiGateway";
    public string Url { get; set; } = "http://localhost:5000";
}
