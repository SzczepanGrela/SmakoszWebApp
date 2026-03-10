using Smakosz.Client.Pages.Public;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages;

public class HomeTests : BunitTestBase
{
    private static HomeDataDto CreateHomeData() => new()
    {
        Stats = new StatsDto
        {
            TotalDishes = 150,
            TotalRestaurants = 25,
            TotalReviews = 500
        },
        TrendingDishes =
        [
            new DishCardDto
            {
                Slug = "pizza-1", DishName = "Pizza Margherita", Price = 25m,
                AvgRating = 9.0, ReviewCount = 10
            }
        ],
        TopRatedDishes =
        [
            new DishCardDto
            {
                Slug = "ramen-1", DishName = "Tonkotsu Ramen", Price = 35m,
                AvgRating = 9.5, ReviewCount = 20
            }
        ],
        RecentReviews =
        [
            new ReviewCardDto
            {
                PublicId = Guid.NewGuid(),
                DishRating = 8, ServiceRating = 7, CleanlinessRating = 9, AmbianceRating = 8,
                Content = "Swietne!",
                Author = new UserSummaryDto { Slug = "jan", Username = "Jan" },
                DishName = "Pizza", DishSlug = "pizza-1",
                RestaurantName = "Roma", RestaurantSlug = "roma"
            }
        ],
        PopularCategories = ["Pizza", "Sushi"],
        HeroImage = new HeroImageDto { Url = "/hero.jpg", CreditText = "Photo credit" }
    };

    [Fact]
    public void LoadingState_ShowsSpinner()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(new TaskCompletionSource<HomeDataDto?>().Task);

        var cut = RenderComponent<Home>();
        cut.Markup.Should().Contain("Ładowanie...");
    }

    [Fact]
    public void DataLoaded_ShowsHeroSection()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();

        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));
        cut.Markup.Should().Contain("Znajdź najlepszy smak w mieście");
        cut.Markup.Should().Contain("Photo credit");
    }

    [Fact]
    public void DataLoaded_ShowsStats()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Markup.Should().Contain("150");
        cut.Markup.Should().Contain("25");
        cut.Markup.Should().Contain("500");
    }

    [Fact]
    public void DataLoaded_ShowsTrendingDishes()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Markup.Should().Contain("Teraz na topie");
        cut.Markup.Should().Contain("Pizza Margherita");
    }

    [Fact]
    public void DataLoaded_ShowsTopRatedDishes()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Markup.Should().Contain("Wysoko oceniane");
        cut.Markup.Should().Contain("Tonkotsu Ramen");
    }

    [Fact]
    public void DataLoaded_ShowsRecentReviews()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Markup.Should().Contain("Najnowsze opinie");
        cut.Markup.Should().Contain("Swietne!");
    }

    [Fact]
    public void DataLoaded_ShowsCategories()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Markup.Should().Contain("Popularne kategorie");
        cut.Markup.Should().Contain("Pizza");
        cut.Markup.Should().Contain("Sushi");
    }

    [Fact]
    public void SearchButton_NavigatesToSearch()
    {
        var homeService = Services.GetRequiredService<IHomeService>();
        homeService.GetHomeDataAsync().Returns(CreateHomeData());

        var cut = RenderComponent<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Ładowanie..."));

        cut.Find("input[type='search']").Input("pizza");
        cut.Find("button.btn-primary.btn-lg").Click();

        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.Uri.Should().Contain("/search?query=pizza&type=dishes");
    }
}
