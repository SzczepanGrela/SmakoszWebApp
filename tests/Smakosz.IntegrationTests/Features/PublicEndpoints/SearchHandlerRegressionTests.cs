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
                BuildRestaurant(1, "Pizza Express", cuisineId: 1, priceLevel: 1),
                BuildRestaurant(2, "Hut Pizza Master", cuisineId: 1, priceLevel: 2),
                BuildRestaurant(3, "Pizzeria Mario", cuisineId: 1, priceLevel: 2),
                BuildRestaurant(4, "Restauracja Włoska Bella", cuisineId: 1, priceLevel: 4),
                BuildRestaurant(5, "Łosoś Bar Warszawa", cuisineId: 1, priceLevel: 3),
                BuildRestaurant(6, "Sushi Tora", cuisineId: 2, priceLevel: 3),
                BuildRestaurant(7, "Bistro pod Lipami", cuisineId: 1, priceLevel: 2),
                BuildRestaurant(8, "Sultan Kebab", cuisineId: 2, priceLevel: 1)
            );

            db.Dishes.AddRange(
                BuildDish(1, "Pizza Margherita", restaurantId: 3, price: 24.90m),
                BuildDish(2, "Pizza Pepperoni", restaurantId: 3, price: 28.50m),
                BuildDish(3, "Łosoś z grilla", restaurantId: 5, price: 49.00m),
                BuildDish(4, "Burger Klasyczny", restaurantId: 8, price: 19.90m),
                BuildDish(5, "Spaghetti Bolognese", restaurantId: 4, price: 32.00m)
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

    [Fact]
    public async Task Sort_Restaurants_NameAsc_OrdersAlphabeticallyByPolishCollation()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants&sortBy=name&sortDir=asc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var names = result!.Restaurants.Select(r => r.RestaurantName).ToArray();
        names.Should().Equal("Bistro pod Lipami", "Hut Pizza Master", "Łosoś Bar Warszawa",
            "Pizza Express", "Pizzeria Mario", "Restauracja Włoska Bella", "Sultan Kebab", "Sushi Tora");
    }

    [Fact]
    public async Task Sort_Restaurants_NameDesc_ReversesPolishAlphabeticOrder()
    {
        var ascResponse = await AnonymousClient.GetAsync("/api/search?type=restaurants&sortBy=name&sortDir=asc&pageSize=50");
        var descResponse = await AnonymousClient.GetAsync("/api/search?type=restaurants&sortBy=name&sortDir=desc&pageSize=50");

        var asc = (await DeserializeResponse<SearchResultDto>(ascResponse))!.Restaurants.Select(r => r.RestaurantName).ToArray();
        var desc = (await DeserializeResponse<SearchResultDto>(descResponse))!.Restaurants.Select(r => r.RestaurantName).ToArray();
        desc.Should().Equal(asc.Reverse());
    }

    [Fact]
    public async Task Sort_Restaurants_PriceAsc_LowestPriceFirst()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants&sortBy=price&sortDir=asc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var prices = result!.Restaurants.Select(r => r.PriceLevel ?? int.MaxValue).ToArray();
        prices.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sort_Restaurants_PriceDesc_HighestPriceFirst()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=restaurants&sortBy=price&sortDir=desc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var prices = result!.Restaurants.Select(r => r.PriceLevel ?? int.MinValue).ToArray();
        prices.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sort_Dishes_NameAsc_OrdersAlphabeticallyByPolishCollation()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=dishes&sortBy=name&sortDir=asc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var names = result!.Dishes.Select(d => d.DishName).ToArray();
        names.Should().HaveElementAt(0, "Burger Klasyczny");
        names.Should().Contain("Łosoś z grilla")
            .And.Contain("Pizza Margherita");
        Array.IndexOf(names, "Łosoś z grilla").Should().BeLessThan(Array.IndexOf(names, "Pizza Margherita"),
            "Polish letter L with stroke must sort between L and M not after Z");
    }

    [Fact]
    public async Task Sort_Dishes_NameDesc_ReversesPolishAlphabeticOrder()
    {
        var ascResponse = await AnonymousClient.GetAsync("/api/search?type=dishes&sortBy=name&sortDir=asc&pageSize=50");
        var descResponse = await AnonymousClient.GetAsync("/api/search?type=dishes&sortBy=name&sortDir=desc&pageSize=50");

        var asc = (await DeserializeResponse<SearchResultDto>(ascResponse))!.Dishes.Select(d => d.DishName).ToArray();
        var desc = (await DeserializeResponse<SearchResultDto>(descResponse))!.Dishes.Select(d => d.DishName).ToArray();
        desc.Should().Equal(asc.Reverse());
    }

    [Fact]
    public async Task Sort_Dishes_PriceAsc_LowestPriceFirst()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=dishes&sortBy=price&sortDir=asc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var prices = result!.Dishes.Select(d => d.Price).ToArray();
        prices.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sort_Dishes_PriceDesc_HighestPriceFirst()
    {
        var response = await AnonymousClient.GetAsync("/api/search?type=dishes&sortBy=price&sortDir=desc&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<SearchResultDto>(response);
        var prices = result!.Dishes.Select(d => d.Price).ToArray();
        prices.Should().BeInDescendingOrder();
    }

    private static Restaurant BuildRestaurant(int id, string name, int cuisineId, int? priceLevel = 2)
    {
        return new Restaurant
        {
            RestaurantId = id,
            PublicId = Guid.CreateVersion7(),
            RestaurantName = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            CuisineTypeId = cuisineId,
            ThemeId = SeedHelpers.FallbackThemeId,
            PriceLevel = priceLevel,
            Address = "ul. Marszalkowska 10",
            CityId = 1,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static Dish BuildDish(int id, string name, int restaurantId, decimal price = 24.90m)
    {
        return new Dish
        {
            DishId = id,
            PublicId = Guid.CreateVersion7(),
            DishName = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            RestaurantId = restaurantId,
            Price = price,
            Calories = 800,
            AvgRating = 8.0,
            ReviewCount = 0,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
