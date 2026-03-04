using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.EdgeCases;

public class NotFoundTests : IntegrationTestBase
{
    [Fact]
    public async Task Restaurant_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/nieistniejaca-restauracja-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dish_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/nieistniejace-danie-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_NonExistent_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/users/nieistniejacy-uzytkownik-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Review_NonExistent_Returns404()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.DeleteAsync($"/api/reviews/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
