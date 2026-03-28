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
            Password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await DeserializeResponse<AuthResult>(response);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/register", new
        {
            Username = "inny-user",
            Email = "jan@smakosz.test",
            Password = "SecurePass123!"
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
            Password = "SecurePass123!"
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
    public async Task Login_ValidCredentials_Returns200()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<AuthResult>(response);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = "WrongPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nieistnieje@smakosz.test",
            Password = SeedHelpers.DefaultPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_BannedUser_Returns403()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "zbanowany@smakosz.test",
            Password = SeedHelpers.DefaultPassword
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
    }
}
