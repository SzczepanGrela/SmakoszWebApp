using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class SearchEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);
        await Factory.SeedDataAsync(db => SeedHelpers.SeedPublicEndpointsScenarioAsync(db, hash));
    }

    [Fact]
    public async Task Search_ReturnsResults()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFilters_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/search/filters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
