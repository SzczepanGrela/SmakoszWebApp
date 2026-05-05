using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class HomeEndpointsTests : IntegrationTestBase
{
    protected override Task SeedAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetHome_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
