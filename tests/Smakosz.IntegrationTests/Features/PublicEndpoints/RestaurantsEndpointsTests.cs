using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class RestaurantsEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);
        await Factory.SeedDataAsync(db => SeedHelpers.SeedPublicEndpointsScenarioAsync(db, hash));
    }

    [Fact]
    public async Task GetRestaurants_ReturnsPaginatedList()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public async Task GetRestaurants_FilterByCuisine_ReturnsFiltered()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants?cuisineType=Turecka");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Sultan Kebab");
        body.Should().NotContain("Pizzeria Roma");
    }

    [Fact]
    public async Task GetBySlug_ReturnsDetails()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/pizzeria-roma");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public async Task GetBySlug_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/nie-istnieje");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDishes_ReturnsDishes()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/pizzeria-roma/dishes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizza Margherita");
    }
}
