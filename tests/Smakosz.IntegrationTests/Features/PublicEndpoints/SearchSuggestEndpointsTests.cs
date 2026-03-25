using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Search.Dtos;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

// ILike is not supported by InMemory provider, so query-matching tests
// are covered by E2E tests against PostgreSQL. These tests cover routing,
// validation, and response format.

public class SearchSuggestEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);
        await Factory.SeedDataAsync(db => SeedHelpers.SeedPublicEndpointsScenarioAsync(db, hash));
    }

    [Fact]
    public async Task Suggest_EmptyQuery_ReturnsEmpty()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_ShortQuery_ReturnsEmpty()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=a");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_NoQueryParam_ReturnsEmpty()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_EndpointExists_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
