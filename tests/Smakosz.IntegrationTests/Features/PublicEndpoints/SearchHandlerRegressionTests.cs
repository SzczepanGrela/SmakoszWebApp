using Smakosz.Application.Features.Search.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

// Pinned fixture set for the queries that ILike used to catch and that pure trigram threatened to drop. Each row asserts a minimum result count for one shape (typo, polish diacritics, common term, prefix). If the threshold is tuned and a row drops below the floor, this test surfaces the regression instead of hiding it behind happy-path assertions.
public class SearchHandlerRegressionTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Cities.Add(SeedHelpers.CreateCity(1, "Warszawa"));

            db.Restaurants.AddRange(
                BuildRestaurant(1, "Pizza Express", cuisineId: 1),
                BuildRestaurant(2, "Hut Pizza Master", cuisineId: 1),
                BuildRestaurant(3, "Pizzeria Mario", cuisineId: 1),
                BuildRestaurant(4, "Restauracja Włoska Bella", cuisineId: 1),
                BuildRestaurant(5, "Łosoś Bar Warszawa", cuisineId: 1),
                BuildRestaurant(6, "Sushi Tora", cuisineId: 2),
                BuildRestaurant(7, "Bistro pod Lipami", cuisineId: 1),
                BuildRestaurant(8, "Sultan Kebab", cuisineId: 2)
            );

            db.Dishes.AddRange(
                BuildDish(1, "Pizza Margherita", restaurantId: 3),
                BuildDish(2, "Pizza Pepperoni", restaurantId: 3),
                BuildDish(3, "Łosoś z grilla", restaurantId: 5),
                BuildDish(4, "Burger Klasyczny", restaurantId: 8),
                BuildDish(5, "Spaghetti Bolognese", restaurantId: 4)
            );

            await db.SaveChangesAsync();
        });
    }

    [Theory]
    [InlineData("Pizza", 3, "common term, multiple matches")]
    [InlineData("pizz", 3, "typo missing last char")]
    [InlineData("PIZZA", 3, "case insensitive")]
    [InlineData("Burger", 1, "single match")]
    [InlineData("Sushi", 1, "exact restaurant name")]
    [InlineData("Mario", 1, "matches restaurant name")]
    [InlineData("bistr", 1, "prefix match short")]
    [InlineData("Łosoś", 1, "polish with diacritics")]
    [InlineData("Spaghetti", 1, "italian dish name")]
    public async Task Search_AllType_ReturnsAtLeastNResults(string query, int minExpected, string scenario)
    {
        var response = await AnonymousClient.GetAsync($"/api/search?type=all&q={Uri.EscapeDataString(query)}&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"scenario: {scenario}");
        var result = await DeserializeResponse<SearchResultDto>(response);
        result.Should().NotBeNull();
        var total = result!.Restaurants.Count + result.Dishes.Count;
        total.Should().BeGreaterThanOrEqualTo(minExpected, $"scenario: {scenario}");
    }

    [Fact]
    public async Task Search_NoQuery_ReturnsAllActiveRestaurants()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        result!.Restaurants.Should().HaveCount(8);
    }

    [Fact]
    public async Task Search_RankingByQuery_PutsExactPrefixHigherThanFuzzyMatch()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants&q=Pizza&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        result!.Restaurants.Should().NotBeEmpty();
        result.Restaurants[0].RestaurantName.Should().StartWith("Pizza");
    }

    private static Restaurant BuildRestaurant(int id, string name, int cuisineId)
    {
        return new Restaurant
        {
            RestaurantId = id,
            PublicId = Guid.NewGuid(),
            RestaurantName = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            CuisineTypeId = cuisineId,
            PriceLevel = 2,
            Address = "ul. Marszalkowska 10",
            CityId = 1,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static Dish BuildDish(int id, string name, int restaurantId)
    {
        return new Dish
        {
            DishId = id,
            PublicId = Guid.NewGuid(),
            DishName = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            RestaurantId = restaurantId,
            Price = 24.90m,
            Calories = 800,
            AvgRating = 8.0,
            ReviewCount = 0,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
