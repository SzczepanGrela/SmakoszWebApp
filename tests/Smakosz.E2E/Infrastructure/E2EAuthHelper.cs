using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Smakosz.E2E.Infrastructure;

public static class E2EAuthHelper
{
    public static string GenerateToken(int userId, string username, string email, string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, username),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateUserToken() =>
        GenerateToken(1, TestConstants.UserUsername, TestConstants.UserEmail, "User");

    public static string GenerateBusinessToken() =>
        GenerateToken(3, TestConstants.BusinessUsername, TestConstants.BusinessEmail, "Restaurant");

    public static string GenerateAdminToken() =>
        GenerateToken(4, "administrator", TestConstants.AdminEmail, "Admin");

    public static string GenerateModeratorToken() =>
        GenerateToken(6, "moderator", "moderator@smakosz.test", "Moderator");
}
