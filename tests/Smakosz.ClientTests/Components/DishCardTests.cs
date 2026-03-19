using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class DishCardTests : BunitTestBase
{
    private static DishCardDto CreateDish() => new()
    {
        PublicId = Guid.NewGuid(),
        Slug = "pizza-margherita",
        DishName = "Pizza Margherita",
        Price = 29.99m,
        AvgRating = 8.5,
        ReviewCount = 42,
        ImageUrl = "/img/pizza.jpg",
        RestaurantName = "Pizzeria Roma",
        IsVegetarian = true,
        IsVegan = false,
        IsGlutenFree = false
    };

    [Fact]
    public void RendersNameAndLink()
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Find("a").GetAttribute("href").Should().Be("/dishes/pizza-margherita");
        cut.Markup.Should().Contain("Pizza Margherita");
    }

    [Fact]
    public void RendersPrice()
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Markup.Should().Contain(29.99m.ToString("F2"));
        cut.Markup.Should().Contain("zł");
    }

    [Fact]
    public void NullPrice_HidesPrice()
    {
        var dish = CreateDish();
        dish.Price = null;
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Markup.Should().NotContain("zł");
    }

    [Fact]
    public void RendersRestaurantName()
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Markup.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public void RendersReviewCount()
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Markup.Should().Contain("(42)");
    }

    [Fact]
    public void RendersDietaryBadges()
    {
        var dish = CreateDish();
        dish.IsVegetarian = true;
        var cut = RenderComponent<DishCard>(p => p.Add(c => c.Dish, dish));

        cut.Markup.Should().Contain("Wege");
    }

    [Fact]
    public void WithBadgeText_RendersBadge()
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p
            .Add(c => c.Dish, dish)
            .Add(c => c.BadgeText, "Trending"));

        cut.Markup.Should().Contain("Trending");
    }

    [Theory]
    [InlineData("small", "dish-card-small")]
    [InlineData("large", "dish-card dish-card-large")]
    [InlineData("normal", "dish-card")]
    public void SizeParameter_SetsCorrectClass(string size, string expectedClass)
    {
        var dish = CreateDish();
        var cut = RenderComponent<DishCard>(p => p
            .Add(c => c.Dish, dish)
            .Add(c => c.Size, size));

        foreach (var cls in expectedClass.Split(' '))
        {
            cut.Find($"div.{cls}").Should().NotBeNull();
        }
    }
}
