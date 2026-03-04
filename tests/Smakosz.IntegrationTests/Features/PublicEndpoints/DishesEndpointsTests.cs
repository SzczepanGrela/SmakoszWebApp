using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class DishesEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);
        await Factory.SeedDataAsync(db => SeedHelpers.SeedPublicEndpointsScenarioAsync(db, hash));
    }

    [Fact]
    public async Task GetBySlug_ReturnsDetails()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/pizza-margherita");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizza Margherita");
    }

    [Fact]
    public async Task GetBySlug_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/nie-istnieje");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRandom_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/random");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReviews_ReturnsPaginated()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/pizza-margherita/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
