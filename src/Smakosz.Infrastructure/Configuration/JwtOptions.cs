namespace Smakosz.Infrastructure.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string? PrivateKeyPath { get; set; }
    public string? PublicKeyPath { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public string ResolvePrivateKey() =>
        !string.IsNullOrEmpty(PrivateKeyPath) ? File.ReadAllText(PrivateKeyPath) : PrivateKey;

    public string ResolvePublicKey() =>
        !string.IsNullOrEmpty(PublicKeyPath) ? File.ReadAllText(PublicKeyPath) : PublicKey;
}
