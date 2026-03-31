using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Moderation;

public class ForbiddenWordsTests : IntegrationTestBase
{
    private Guid _dishPublicId;
    private Guid _reviewPublicId;
    private int _menuSectionId;

    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            SeedHelpers.SeedForbiddenWords(db);

            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var businessUser = SeedHelpers.CreateBusinessUser(50, hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId, ownerId: 50);
            var dish = SeedHelpers.CreateDish(1, "Pizza Margherita", restaurant.RestaurantId, 24.90m);
            var menuSection = SeedHelpers.CreateMenuSection(1, restaurant.RestaurantId, "Pizze");
            var review = SeedHelpers.CreateReview(1, user.UserId, dish.DishId, restaurant.RestaurantId);

            db.Cities.Add(city);
            db.Users.AddRange(user, businessUser);
            db.Restaurants.Add(restaurant);
            db.Dishes.Add(dish);
            db.MenuSections.Add(menuSection);
            db.Reviews.Add(review);
            await db.SaveChangesAsync();

            businessUser.RestaurantId = restaurant.RestaurantId;
            await db.SaveChangesAsync();

            _dishPublicId = dish.PublicId;
            _reviewPublicId = review.PublicId;
            _menuSectionId = menuSection.SectionId;
        });
    }

    [Fact]
    public async Task CreateDish_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/business/dishes", new
        {
            DishName = "Pizza kurwa",
            Price = 25.00,
            IsAvailable = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateDish_WithCleanText_Returns201()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/business/dishes", new
        {
            DishName = "Pizza Hawajska",
            Price = 28.00,
            Description = "Szynka i ananas",
            IsAvailable = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateDish_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PutAsJsonAsync($"/api/business/dishes/{_dishPublicId}", new
        {
            DishName = "chuj pizza"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateMenuSection_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/business/menu-sections", new
        {
            Name = "fuck sekcja"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateMenuSection_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PutAsJsonAsync($"/api/business/menu-sections/{_menuSectionId}", new
        {
            Name = "shit sekcja"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateReview_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        await Factory.SeedDataAsync(async db =>
        {
            var dish2 = SeedHelpers.CreateDish(2, "Pizza Pepperoni", 1, 29.90m);
            db.Dishes.Add(dish2);
            await db.SaveChangesAsync();
        });

        Guid dish2PublicId = default;
        await Factory.SeedDataAsync(async db =>
        {
            var dish2 = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(db.Dishes, d => d.DishId == 2);
            dish2PublicId = dish2.PublicId;
        });

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            DishPublicId = dish2PublicId,
            DishRating = 8,
            ServiceRating = 7,
            CleanlinessRating = 8,
            AmbianceRating = 7,
            Content = "Jebane dobre",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateReview_WithForbiddenWord_Returns422()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PutAsJsonAsync($"/api/reviews/{_reviewPublicId}", new
        {
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 8,
            Content = "Kurwa jakie dobre",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateMenuSection_WithCleanText_Returns201()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/business/menu-sections", new
        {
            Name = "Desery"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
