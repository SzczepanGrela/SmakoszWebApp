using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class SearchTypeSliderTests : BunitTestBase
{
    [Fact]
    public void RendersRestaurantsAndDishesButtons()
    {
        var cut = RenderComponent<SearchTypeSlider>();

        cut.FindAll("button").Should().HaveCount(2);
        cut.Markup.Should().Contain("Restauracje");
        cut.Markup.Should().Contain("Dania");
    }

    [Fact]
    public void RestaurantsActive_RestaurantButtonPrimary()
    {
        var cut = RenderComponent<SearchTypeSlider>(p => p
            .Add(c => c.ActiveType, "restaurants"));

        var buttons = cut.FindAll("button");
        buttons[0].ClassList.Should().Contain("btn-primary");
        buttons[1].ClassList.Should().Contain("btn-outline-secondary");
    }

    [Fact]
    public void DishesActive_DishButtonPrimary()
    {
        var cut = RenderComponent<SearchTypeSlider>(p => p
            .Add(c => c.ActiveType, "dishes"));

        var buttons = cut.FindAll("button");
        buttons[0].ClassList.Should().Contain("btn-outline-secondary");
        buttons[1].ClassList.Should().Contain("btn-primary");
    }

    [Fact]
    public void ClickDishes_InvokesOnTypeChange()
    {
        string? selected = null;
        var cut = RenderComponent<SearchTypeSlider>(p => p
            .Add(c => c.ActiveType, "restaurants")
            .Add(c => c.OnTypeChange, (string v) => selected = v));

        cut.FindAll("button")[1].Click(); // Dania button
        selected.Should().Be("dishes");
    }

    [Fact]
    public void ClickRestaurants_InvokesOnTypeChange()
    {
        string? selected = null;
        var cut = RenderComponent<SearchTypeSlider>(p => p
            .Add(c => c.ActiveType, "dishes")
            .Add(c => c.OnTypeChange, (string v) => selected = v));

        cut.FindAll("button")[0].Click(); // Restauracje button
        selected.Should().Be("restaurants");
    }
}
