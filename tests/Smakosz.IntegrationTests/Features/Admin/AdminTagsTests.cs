using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Admin;

public class AdminTagsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateAdminUser(99, hash));
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            db.SiteStats.Add(SeedHelpers.CreateSiteStats());
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task CreateTag_AsAdmin_Returns201()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.PostAsJsonAsync("/api/admin/tags", new
        {
            Name = "Na wynos",
            Category = "Typ",
            TargetEntity = "Both"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTag_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/admin/tags", new
        {
            Name = "Sezonowe",
            Category = "Typ",
            TargetEntity = "Dish"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTag_AsAdmin_Returns204()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var createResponse = await client.PostAsJsonAsync("/api/admin/tags", new
        {
            Name = "DoUsuniecia",
            Category = "Test",
            TargetEntity = "Both"
        });
        var created = await DeserializeResponse<int>(createResponse);

        var response = await client.DeleteAsync($"/api/admin/tags/{created}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
