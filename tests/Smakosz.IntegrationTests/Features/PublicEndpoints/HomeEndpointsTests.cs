using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class HomeEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.SiteStats.Add(SeedHelpers.CreateSiteStats());
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetHome_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
