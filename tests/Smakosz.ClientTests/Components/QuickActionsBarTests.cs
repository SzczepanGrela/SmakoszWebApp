using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class QuickActionsBarTests : BunitTestBase
{
    [Fact]
    public void RendersRandomDishButton()
    {
        var cut = RenderComponent<QuickActionsBar>();

        cut.Markup.Should().Contain("Losowe danie");
    }

    [Fact]
    public void Anonymous_NoRecommendationsLink()
    {
        var cut = RenderComponent<QuickActionsBar>();
        cut.FindAll("a[href='/recommendations']").Should().BeEmpty();
    }

    [Fact]
    public void Authenticated_ShowsRecommendationsLink()
    {
        SetAuthenticatedUser("testuser", "User");
        var cut = RenderComponent<QuickActionsBar>();

        cut.Find("a[href='/recommendations']").Should().NotBeNull();
        cut.Markup.Should().Contain("Rekomendacje");
    }

    [Fact]
    public async Task ClickRandomDish_NavigatesToDish()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetRandomAsync().Returns(new DishCardDto { Slug = "random-dish" });

        var cut = RenderComponent<QuickActionsBar>();
        var randomBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Losowe danie"));
        randomBtn.Click();

        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.Uri.Should().Contain("/dishes/random-dish");
    }

}
