using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class RestaurantCardTests : BunitTestBase
{
    private static RestaurantCardDto CreateRestaurant() => new()
    {
        PublicId = Guid.NewGuid(),
        Slug = "pizzeria-roma",
        RestaurantName = "Pizzeria Roma",
        CuisineType = "Wloska",
        CityName = "Warszawa",
        PriceLevel = 3,
        AvgFoodScore = 8.5,
        ReviewCount = 120
    };

    [Fact]
    public void RendersNameAndLink()
    {
        var restaurant = CreateRestaurant();
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.Find("a").GetAttribute("href").Should().Be("/restaurant/pizzeria-roma");
        cut.Markup.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public void RendersCuisineType()
    {
        var restaurant = CreateRestaurant();
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.Markup.Should().Contain("Wloska");
    }

    [Fact]
    public void RendersCityName()
    {
        var restaurant = CreateRestaurant();
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.Markup.Should().Contain("Warszawa");
    }

    [Fact]
    public void RendersPriceLevel()
    {
        var restaurant = CreateRestaurant();
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.Markup.Should().Contain("$$$");
    }

    [Fact]
    public void RendersReviewCount()
    {
        var restaurant = CreateRestaurant();
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.Markup.Should().Contain("(120)");
    }

    [Fact]
    public void NoCuisine_HidesCuisine()
    {
        var restaurant = CreateRestaurant();
        restaurant.CuisineType = null;
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.FindAll("i.fa-solid.fa-utensils").Should().BeEmpty();
    }

    [Fact]
    public void NoRating_HidesRating()
    {
        var restaurant = CreateRestaurant();
        restaurant.AvgFoodScore = null;
        var cut = RenderComponent<RestaurantCard>(p => p.Add(c => c.Restaurant, restaurant));

        cut.FindAll(".rating-stars").Should().BeEmpty();
    }
}
