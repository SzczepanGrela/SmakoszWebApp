using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T99_DishCategoryFilterTest : SmakoszE2ETestBase
{
    [Test]
    public async Task CategoryFilter_VisibleOnDishesType_FiltersResults()
    {
        await NavigateAndWaitAsync("/search?type=dishes");

        var filtersToggle = Page.Locator(".filters-panel h5", new() { HasTextString = "Filtry" }).First;
        await Expect(filtersToggle).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await filtersToggle.ClickAsync();

        var categoryHeader = Page.Locator("label.form-label", new() { HasTextString = "Kategoria dania" }).First;
        await Expect(categoryHeader).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var pizzaRow = Page.Locator(".form-check").Filter(new() { Has = Page.Locator("label.form-check-label", new() { HasTextString = "Pizza" }) }).First;
        await Expect(pizzaRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await pizzaRow.Locator("input.form-check-input").CheckAsync();

        var applyButton = Page.Locator("button", new() { HasTextString = "Zastosuj filtry" }).First;
        await applyButton.ClickAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("dishCategories=Pizza"),
            new PageWaitForURLOptions { Timeout = 10_000 });

        await WaitForBlazorLoadedAsync();

        var pizzaBadges = Page.Locator(".dish-card .badge", new() { HasTextString = "Pizza" });
        var badgeCount = await pizzaBadges.CountAsync();
        Assert.That(badgeCount, Is.GreaterThan(0),
            "Expected at least one dish card with Pizza category badge after filter");
    }

    [Test]
    public async Task CategoryFilter_HiddenOnRestaurantsType()
    {
        await NavigateAndWaitAsync("/search?type=restaurants");

        var filtersToggle = Page.Locator(".filters-panel h5", new() { HasTextString = "Filtry" }).First;
        if (await filtersToggle.IsVisibleAsync())
        {
            await filtersToggle.ClickAsync();
        }

        var categoryHeader = Page.Locator("label.form-label", new() { HasTextString = "Kategoria dania" });
        await Expect(categoryHeader).ToHaveCountAsync(0);
    }
}
