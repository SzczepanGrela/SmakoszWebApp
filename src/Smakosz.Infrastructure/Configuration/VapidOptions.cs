namespace Smakosz.Infrastructure.Configuration;

public class VapidOptions
{
    public const string SectionName = "Vapid";

    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = "mailto:push@smakosz.xyz";
}
