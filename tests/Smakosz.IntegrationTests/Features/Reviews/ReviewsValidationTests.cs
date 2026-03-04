using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Reviews;

public class ReviewsValidationTests : IntegrationTestBase
{
    private Guid _dishPublicId;

    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId);
            var dish = SeedHelpers.CreateDish(1, "Pizza Margherita", restaurant.RestaurantId, 24.90m);

            db.Cities.Add(city);
            db.Users.Add(user);
            db.Restaurants.Add(restaurant);
            db.Dishes.Add(dish);
            await db.SaveChangesAsync();

            _dishPublicId = dish.PublicId;
        });
    }

    [Fact]
    public async Task Create_RatingOutOfRange_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 15,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 7,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_EmptyDishId_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = Guid.Empty,
            DishRating = 8,
            ServiceRating = 8,
            CleanlinessRating = 8,
            AmbianceRating = 8,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_FutureVisitDate_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 8,
            ServiceRating = 8,
            CleanlinessRating = 8,
            AmbianceRating = 8,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ContentTooShort_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = _dishPublicId,
            DishRating = 8,
            ServiceRating = 8,
            CleanlinessRating = 8,
            AmbianceRating = 8,
            Content = "abc",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
