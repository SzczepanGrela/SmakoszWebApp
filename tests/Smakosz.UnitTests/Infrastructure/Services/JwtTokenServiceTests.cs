using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Configuration;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class JwtTokenServiceTests
{
    private const string TestSecret = "SuperSecretKeyForTestingPurposesThatIsLongEnoughForHmacSha256!";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Secret = TestSecret,
            Issuer = TestIssuer,
            Audience = TestAudience,
        });

        _sut = new JwtTokenService(options);
    }

    private static UserBuilder DefaultUser() =>
        new UserBuilder()
            .WithId(42)
            .WithEmail("jwt@test.com")
            .WithUsername("jwtuser")
            .WithRole(UserRole.User);

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwtString()
    {
        var user = DefaultUser().Build();

        var token = _sut.GenerateAccessToken(user);

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void GenerateAccessToken_ContainsCorrectClaims()
    {
        var user = DefaultUser().Build();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "jwt@test.com");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "jwtuser");
    }

    [Fact]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var user = DefaultUser().Build();
        var before = DateTime.UtcNow;

        var token = _sut.GenerateAccessToken(user);
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(before.AddMinutes(14));
        jwt.ValidTo.Should().BeBefore(after.AddMinutes(16));
    }

    [Fact]
    public void GenerateAccessToken_HasCorrectIssuerAndAudience()
    {
        var user = DefaultUser().Build();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(TestIssuer);
        jwt.Audiences.Should().Contain(TestAudience);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64String()
    {
        var token = _sut.GenerateRefreshToken();

        token.Should().NotBeNullOrEmpty();
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsDifferentTokensEachCall()
    {
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_Has64BytesLength()
    {
        var token = _sut.GenerateRefreshToken();

        var bytes = Convert.FromBase64String(token);
        bytes.Should().HaveCount(64);
        token.Should().HaveLength(88);
    }
}
