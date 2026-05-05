using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.RoleAccess;

public class AdminAccessTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateAdminUser(99, hash));
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Dashboard_AsAdmin_Returns200()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_Anonymous_Returns401()
    {
        var response = await AnonymousClient.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_AsAdmin_Returns200()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BanUser_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        // Use a random GUID - the important thing is the 403, not finding the user
        var response = await client.PostAsync($"/api/admin/users/{Guid.NewGuid()}/ban", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCity_AsAdmin_Returns201()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PostAsJsonAsync("/api/admin/cities", new
        {
            Name = "Gdansk",
            Region = "Pomorskie"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateCity_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/admin/cities", new
        {
            Name = "Wroclaw",
            Region = "Dolnoslaskie"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
