using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T81_DishTagsDisplayTest : SmakoszE2ETestBase
{
    [Test]
    public async Task AnonymousUser_CanSeeTagsOnDishPage_AndClickToSearch()
    {
        await NavigateAndWaitAsync("/dishes/pizza-margherita");
        await WaitForBlazorLoadedAsync();

        var tagContainer = Page.Locator(".dish-tags");
        await Expect(tagContainer).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Na wynos"), Is.True, "Tag 'Na wynos' should be visible on Pizza Margherita");
        Assert.That(pageContent.Contains("Sezonowe"), Is.True, "Tag 'Sezonowe' should be visible on Pizza Margherita");

        var naWynosTag = Page.Locator(".dish-tags .badge", new() { HasTextString = "Na wynos" }).First;
        await Expect(naWynosTag).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await naWynosTag.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/search"), new PageWaitForURLOptions { Timeout = 10_000 });
        Assert.That(Page.Url, Does.Contain("tags="), "URL should contain tags parameter");

        await WaitForBlazorLoadedAsync();
    }
}
