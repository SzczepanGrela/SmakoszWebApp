using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Smakosz.API.Services;

namespace Smakosz.UnitTests.API.Services;

[Trait("Category", "Services")]
public class CurrentUserServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _sut = new CurrentUserService(_httpContextAccessor);
    }

    private void SetupUser(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    [Fact]
    public void UserId_WithSubClaim_ReturnsId()
    {
        SetupUser(new Claim(JwtRegisteredClaimNames.Sub, "42"));

        var result = _sut.UserId;

        result.Should().Be(42);
    }

    [Fact]
    public void UserId_WithNameIdentifierClaim_ReturnsId()
    {
        SetupUser(new Claim(ClaimTypes.NameIdentifier, "99"));

        var result = _sut.UserId;

        result.Should().Be(99);
    }

    [Fact]
    public void UserId_NoClaims_ReturnsNull()
    {
        SetupUser();

        var result = _sut.UserId;

        result.Should().BeNull();
    }

    [Fact]
    public void UserId_InvalidClaimValue_ReturnsNull()
    {
        SetupUser(new Claim(JwtRegisteredClaimNames.Sub, "not-a-number"));

        var result = _sut.UserId;

        result.Should().BeNull();
    }

    [Fact]
    public void Role_WithRoleClaim_ReturnsRole()
    {
        SetupUser(new Claim(ClaimTypes.Role, "admin"));

        var result = _sut.Role;

        result.Should().Be("admin");
    }

    [Fact]
    public void IsAuthenticated_AuthenticatedUser_ReturnsTrue()
    {
        SetupUser(new Claim(ClaimTypes.Role, "user"));

        var result = _sut.IsAuthenticated;

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_NoHttpContext_ReturnsFalse()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var result = _sut.IsAuthenticated;

        result.Should().BeFalse();
    }
}
