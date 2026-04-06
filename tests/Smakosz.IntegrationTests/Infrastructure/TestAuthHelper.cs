using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Smakosz.IntegrationTests.Infrastructure;

public static class TestAuthHelper
{
    private static readonly RSA Rsa = RSA.Create(2048);

    public static string JwtPrivateKey { get; } = Rsa.ExportRSAPrivateKeyPem();
    public static string JwtPublicKey { get; } = Rsa.ExportRSAPublicKeyPem();

    public const string JwtIssuer = "Smakosz.API";
    public const string JwtAudience = "Smakosz.Client";

    public static string WorkerApiKey =>
        Environment.GetEnvironmentVariable("SMAKOSZ_WORKER_API_KEY")
        ?? "test-worker-api-key-for-integration-tests";

    public static string GenerateJwtToken(
        int userId,
        string username,
        string email,
        string role,
        TimeSpan? expiry = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, username),
        };

        using var rsa = RSA.Create();
        rsa.ImportFromPem(JwtPrivateKey);
        var key = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(15)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateExpiredToken(int userId = 1)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(JwtRegisteredClaimNames.Email, "expired@smakosz.test"),
            new Claim(JwtRegisteredClaimNames.Name, "expired-user"),
        };

        using var rsa = RSA.Create();
        rsa.ImportFromPem(JwtPrivateKey);
        var key = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-5),
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
