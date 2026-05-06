using Smakosz.Application.Features.Search.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

// Verifies the trigram-backed suggest pipeline end to end against real Postgres: typo tolerance, polish diacritics, prefix boost, and limit clamping. Pinned to /api/search/suggest so any future routing or DI regression is caught.
public class SearchSuggestTrigramTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Cities.Add(SeedHelpers.CreateCity(1, "Warszawa"));

            db.Restaurants.AddRange(
                BuildRestaurant(1, "Pizzeria Roma", cuisineId: 1),
                BuildRestaurant(2, "Hut Pizza Master", cuisineId: 1),
                BuildRestaurant(3, "Losos Bar Warszawa", cuisineId: 1),
                BuildRestaurant(4, "Sultan Kebab", cuisineId: 2)
            );

            db.Dishes.AddRange(
                BuildDish(1, "Pizza Margherita", restaurantId: 1),
                BuildDish(2, "Pizza Pepperoni", restaurantId: 1),
                BuildDish(3, "Łosoś z grilla", restaurantId: 3),
                BuildDish(4, "Kebab Duzy", restaurantId: 4)
            );

            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Suggest_PrefixMatch_ReturnsItemsStartingWithTerm()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=Pizza");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items!.Should().NotBeEmpty();
        items![0].Name.Should().StartWith("Pizza");
    }

    [Fact]
    public async Task Suggest_Typo_StillReturnsResults()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=pizz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items!.Should().NotBeEmpty();
        items.Should().Contain(i => i.Name.Contains("Pizza", StringComparison.OrdinalIgnoreCase) || i.Name.Contains("Pizzeria", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Suggest_PolishDiacritics_MatchUnaccentedQuery()
    {
        var unaccented = await AnonymousClient.GetAsync("/api/search/suggest?q=losos");
        var accented = await AnonymousClient.GetAsync("/api/search/suggest?q=%C5%82oso%C5%9B");

        unaccented.StatusCode.Should().Be(HttpStatusCode.OK);
        accented.StatusCode.Should().Be(HttpStatusCode.OK);

        var unaccentedItems = await DeserializeResponse<List<SuggestItemDto>>(unaccented);
        var accentedItems = await DeserializeResponse<List<SuggestItemDto>>(accented);

        unaccentedItems!.Should().NotBeEmpty();
        accentedItems!.Should().NotBeEmpty();
        unaccentedItems.Should().Contain(i => i.Name.Contains("Łosoś", StringComparison.Ordinal) || i.Name.Contains("Losos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Suggest_RespectsLimit()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=Pizza&limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items!.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Suggest_NoMatch_ReturnsEmpty()
    {
        var response = await AnonymousClient.GetAsync("/api/search/suggest?q=xyzabcq");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await DeserializeResponse<List<SuggestItemDto>>(response);
        items.Should().NotBeNull();
        items!.Should().BeEmpty();
    }

    private static Restaurant BuildRestaurant(int id, string name, int cuisineId)
    {
        return new Restaurant
        {
            RestaurantId = id,
            PublicId = Guid.CreateVersion7(),
            RestaurantName = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            CuisineTypeId = cuisineId,
            ThemeId = SeedHelpers.FallbackThemeId,
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
            PublicId = Guid.CreateVersion7(),
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
