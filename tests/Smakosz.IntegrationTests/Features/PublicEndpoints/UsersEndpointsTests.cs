using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class UsersEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetBySlug_ReturnsProfile()
    {
        var response = await AnonymousClient.GetAsync("/api/users/jan-kowalski");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("jan-kowalski");
    }

    [Fact]
    public async Task GetBySlug_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/users/nie-istnieje");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
