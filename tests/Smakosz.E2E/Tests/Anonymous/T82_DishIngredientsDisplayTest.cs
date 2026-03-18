using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T82_DishIngredientsDisplayTest : SmakoszE2ETestBase
{
    [Test]
    public async Task AnonymousUser_CanSeeIngredientsOnDishPage_WithAllergenBadges()
    {
        await NavigateAndWaitAsync("/dishes/pizza-margherita");
        await WaitForBlazorLoadedAsync();

        // Ingredients section is collapsed by default
        var ingredientsHeader = Page.GetByText("Skladniki (4)").First;
        await Expect(ingredientsHeader).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await ingredientsHeader.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Maka pszenna"), Is.True, "Ingredient 'Maka pszenna' should be visible");
        Assert.That(pageContent.Contains("Ser mozzarella"), Is.True, "Ingredient 'Ser mozzarella' should be visible");
        Assert.That(pageContent.Contains("Sos pomidorowy"), Is.True, "Ingredient 'Sos pomidorowy' should be visible");
        Assert.That(pageContent.Contains("Bazylia"), Is.True, "Ingredient 'Bazylia' should be visible");

        var allergenBadges = Page.Locator(".badge", new() { HasTextString = "Alergen" });
        var allergenCount = await allergenBadges.CountAsync();
        Assert.That(allergenCount, Is.GreaterThanOrEqualTo(2), "At least 2 allergen badges (Maka pszenna, Ser mozzarella)");

        var glutenBadge = Page.Locator(".badge", new() { HasTextString = "Gluten" });
        var glutenCount = await glutenBadge.CountAsync();
        Assert.That(glutenCount, Is.GreaterThanOrEqualTo(1), "Gluten badge should appear for Maka pszenna");

        var laktozaBadge = Page.Locator(".badge", new() { HasTextString = "Laktoza" });
        var laktozaCount = await laktozaBadge.CountAsync();
        Assert.That(laktozaCount, Is.GreaterThanOrEqualTo(1), "Laktoza badge should appear for Ser mozzarella");
    }
}
