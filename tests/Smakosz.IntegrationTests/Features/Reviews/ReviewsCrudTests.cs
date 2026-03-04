using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Reviews;

public class ReviewsCrudTests : IntegrationTestBase
{
    private Guid _dishPublicId;
    private Guid _reviewPublicId;

    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var user2 = SeedHelpers.CreateUser(2, "anna-nowak", "anna@smakosz.test", hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId);
            var dish = SeedHelpers.CreateDish(1, "Pizza Margherita", restaurant.RestaurantId, 24.90m);
            var dish2 = SeedHelpers.CreateDish(2, "Pizza Pepperoni", restaurant.RestaurantId, 29.90m);

            var existingReview = SeedHelpers.CreateReview(1, user2.UserId, dish.DishId, restaurant.RestaurantId);

            db.Cities.Add(city);
            db.Users.AddRange(user, user2);
            db.Restaurants.Add(restaurant);
            db.Dishes.AddRange(dish, dish2);
            db.Reviews.Add(existingReview);
            await db.SaveChangesAsync();

            _dishPublicId = dish.PublicId;
            _reviewPublicId = existingReview.PublicId;
        });
    }

    [Fact]
    public async Task Create_WithAuth_Returns201()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 7,
            Content = "Doskonala pizza, polecam kazdemu!",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 7,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        using var client = Factory.CreateUserClient(2, "anna-nowak");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 7,
            ServiceRating = 6,
            CleanlinessRating = 7,
            AmbianceRating = 6,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_AsOwner_Returns200()
    {
        using var client = Factory.CreateUserClient(2, "anna-nowak");

        var response = await client.PutAsJsonAsync($"/api/reviews/{_reviewPublicId}", new
        {
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 8,
            Content = "Zaktualizowana recenzja - jeszcze lepsza!",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_AsNonOwner_Returns403()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PutAsJsonAsync($"/api/reviews/{_reviewPublicId}", new
        {
            DishRating = 1,
            ServiceRating = 1,
            CleanlinessRating = 1,
            AmbianceRating = 1,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_AsOwner_Returns204()
    {
        using var client = Factory.CreateUserClient(2, "anna-nowak");

        var response = await client.DeleteAsync($"/api/reviews/{_reviewPublicId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.DeleteAsync($"/api/reviews/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
