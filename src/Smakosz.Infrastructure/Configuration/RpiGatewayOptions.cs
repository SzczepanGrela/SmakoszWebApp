namespace Smakosz.Infrastructure.Configuration;

public class RpiGatewayOptions
{
    public const string SectionName = "RpiGateway";
    public string Url { get; set; } = "http://localhost:5000";
    public string ApiToken { get; set; } = "";
}
