using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Me;

public class MeEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user1 = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var user2 = SeedHelpers.CreateUser(2, "anna-nowak", "anna@smakosz.test", hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId);
            var dish = SeedHelpers.CreateDish(1, "Pizza Margherita", restaurant.RestaurantId, 24.90m);

            db.Cities.Add(city);
            db.Users.AddRange(user1, user2);
            db.Restaurants.Add(restaurant);
            db.Dishes.Add(dish);
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetProfile_Returns200()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfile_Unauthorized_Returns401()
    {
        var response = await AnonymousClient.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveDish_Returns204()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsync("/api/me/saved-dishes/pizza-margherita", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SaveDish_Already_Returns409()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        await client.PostAsync("/api/me/saved-dishes/pizza-margherita", null);

        // Try saving again
        var response = await client.PostAsync("/api/me/saved-dishes/pizza-margherita", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnsaveDish_Returns204()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        await client.PostAsync("/api/me/saved-dishes/pizza-margherita", null);

        // Unsave
        var response = await client.DeleteAsync("/api/me/saved-dishes/pizza-margherita");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FollowUser_Returns204()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsync("/api/me/following/anna-nowak", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FollowSelf_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsync("/api/me/following/jan-kowalski", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task FavoriteRestaurant_Returns204()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsync("/api/me/favorite-restaurants/pizzeria-roma", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
