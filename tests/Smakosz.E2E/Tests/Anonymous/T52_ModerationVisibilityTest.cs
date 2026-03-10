using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T52_ModerationVisibilityTest : SmakoszE2ETestBase
{
    [Test]
    public async Task PublicUser_DoesNotSeePendingDish()
    {
        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();

        Assert.That(pageContent.Contains("Pizza Margherita"), Is.True,
            "Approved dish 'Pizza Margherita' should be visible on restaurant page");

        Assert.That(pageContent.Contains("Pizza Testowa Pending"), Is.False,
            "Pending dish 'Pizza Testowa Pending' should NOT be visible to public users");
    }

    [Test]
    public async Task PublicUser_DoesNotSeePendingMenuSection()
    {
        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();

        Assert.That(pageContent.Contains("Pizze") || pageContent.Contains("Desery"), Is.True,
            "Approved menu sections should be visible");

        Assert.That(pageContent.Contains("Sekcja Pending"), Is.False,
            "Pending menu section should NOT be visible to public users");
    }

    [Test]
    public async Task PublicUser_PendingDishSlug_Returns404()
    {
        await NavigateAndWaitAsync("/dishes/pizza-testowa-pending");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();

        Assert.That(
            pageContent.Contains("404") || pageContent.Contains("Nie znaleziono") ||
            pageContent.Contains("nie istnieje") || !pageContent.Contains("Pizza Testowa Pending"),
            Is.True, "Pending dish should not be accessible by slug");
    }
}
