using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Auth;

public class AuthEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            db.Users.Add(SeedHelpers.CreateBannedUser(10, hash));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Register_ValidData_Returns200()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "nowy-uzytkownik",
            Email = "nowy@smakosz.test",
            Password = "SecurePass123!",
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "inny-user",
            Email = "jan@smakosz.test",
            Password = "SecurePass123!",
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns409()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "jan-kowalski",
            Email = "inny@smakosz.test",
            Password = "SecurePass123!",
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns422()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "testuser",
            Email = "nie-email",
            Password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns422()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "testuser2",
            Email = "test2@smakosz.test",
            Password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_ValidCredentials_SetsHttpOnlyCookiesAndOmitsTokensFromBody()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookieList = cookies!.ToList();
        cookieList.Should().Contain(c => c.StartsWith("sm_at=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        cookieList.Should().Contain(c => c.StartsWith("sm_rt=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var result = await DeserializeResponse<AuthResult>(response);
        result.Should().NotBeNull();
        result!.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
        result.User.Email.Should().Be("jan@smakosz.test");
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesAndReturnsNewCookies()
    {
        using var client = Factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResp = await client.PostAsync("/api/auth/refresh", content: null);
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshResp.Headers.TryGetValues("Set-Cookie", out var newCookies).Should().BeTrue();
        newCookies!.ToList().Should().Contain(c => c.StartsWith("sm_at="));
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await AnonymousClient.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await DeserializeError(response);
        error!.Code.Should().Be("REFRESH_TOKEN_MISSING");
    }

    [Fact]
    public async Task Logout_WithCookie_DeletesCookies()
    {
        using var client = Factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        var logoutResp = await client.PostAsync("/api/auth/logout", content: null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logoutResp.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookieList = cookies!.ToList();
        cookieList.Should().Contain(c => c.StartsWith("sm_at=") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        cookieList.Should().Contain(c => c.StartsWith("sm_rt=") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Me_WithValidCookie_ReturnsClaims()
    {
        using var client = Factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        var meResp = await client.GetAsync("/api/auth/me");
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await meResp.Content.ReadAsStringAsync();
        json.Should().Contain("jan@smakosz.test");
        json.Should().Contain("\"role\"");
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        var response = await AnonymousClient.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = "WrongPassword123!",
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nieistnieje@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_BannedUser_Returns403()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "zbanowany@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var response = await AnonymousClient.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ExpiredToken_Returns401()
    {
        var client = Factory.CreateAnonymousClient();
        var expiredToken = TestAuthHelper.GenerateExpiredToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record AuthResult
    {
        public string AccessToken { get; init; } = default!;
        public string RefreshToken { get; init; } = default!;
        public DateTime ExpiresAt { get; init; }
        public UserDto User { get; init; } = default!;
    }

    private record UserDto
    {
        public string Email { get; init; } = default!;
        public string Username { get; init; } = default!;
        public string Role { get; init; } = default!;
    }
}
