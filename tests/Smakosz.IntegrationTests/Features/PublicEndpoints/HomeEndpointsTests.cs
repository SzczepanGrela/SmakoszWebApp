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

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Got 500. Response body: {body}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
