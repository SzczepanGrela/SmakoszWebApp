namespace Smakosz.Infrastructure.Configuration;

public class RbpiGatewayOptions
{
    public const string SectionName = "RbpiGateway";
    public string Url { get; set; } = "http://localhost:5000";
    public string ApiToken { get; set; } = "";
}
