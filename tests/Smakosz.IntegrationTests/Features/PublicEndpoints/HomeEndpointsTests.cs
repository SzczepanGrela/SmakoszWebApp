using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class HomeEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task GetHome_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
