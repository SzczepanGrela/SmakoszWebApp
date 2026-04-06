using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Smakosz.IntegrationTests.Infrastructure;

public static class TestAuthHelper
{
    public static string JwtSecret =>
        Environment.GetEnvironmentVariable("SMAKOSZ_JWT_SECRET")
        ?? "***REMOVED***";

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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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
