using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T122_AdminUserDetailReviewsAccordionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_OpensReviewsAccordion_AndPaginates()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/users?search=anna-nowak");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var annaRow = Page.Locator("tr", new() { HasText = "anna-nowak" }).First;
        await Expect(annaRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var detailLink = annaRow.Locator("a[href*='/admin/users/']").First;
        await detailLink.ClickAsync();

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(3000);

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("anna-nowak", new LocatorAssertionsToContainTextOptions { Timeout = 10_000 });

        var reviewsHeader = Page.Locator(".card-header", new() { HasText = "Recenzje" }).First;
        await Expect(reviewsHeader).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await reviewsHeader.ClickAsync();

        await Page.WaitForTimeoutAsync(2500);
        await WaitForBlazorLoadedAsync();

        var rows = Page.Locator("a[href^='/admin/dishes/']");
        var initialCount = await rows.CountAsync();
        Assert.That(initialCount, Is.GreaterThanOrEqualTo(10), $"Expected at least 10 review rows on first page, got {initialCount}");

        var loadMore = Page.Locator("button", new() { HasText = "Załaduj więcej" }).First;
        await Expect(loadMore).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await loadMore.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var afterCount = await rows.CountAsync();
        Assert.That(afterCount, Is.GreaterThan(initialCount), $"Expected row count to increase after Załaduj więcej (was {initialCount}, now {afterCount})");
    }
}
