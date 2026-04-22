using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.RoleAccess;

public class BusinessAccessTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            SeedHelpers.SeedDishCategoryTags(db);

            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var businessUser = SeedHelpers.CreateBusinessUser(50, hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId, ownerId: 50);
            var regularUser = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);

            db.Cities.Add(city);
            db.Users.AddRange(businessUser, regularUser);
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync();

            businessUser.RestaurantId = restaurant.RestaurantId;
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Dashboard_AsBusiness_Returns200()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.GetAsync("/api/business/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_AsUser_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/business/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRestaurant_AsBusiness_Returns200()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.GetAsync("/api/business/restaurant");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateDish_AsBusiness_Returns201()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/business/dishes", new
        {
            DishName = "Spaghetti Bolognese",
            Price = 32.50,
            Description = "Klasyczne spaghetti z sosem bolonskim",
            Calories = 650,
            IsAvailable = true,
            DishCategoryTagName = "Makaron"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
