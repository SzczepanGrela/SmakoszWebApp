using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T121_AdminRestaurantClaimsQueueTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_RestaurantClaimsQueue_ShowsList_AndReturnUrlRoundTrip()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/restaurant-claims");
        await WaitForBlazorLoadedAsync();

        var table = Page.Locator("table");
        await Expect(table).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var firstOpenBtn = Page.GetByRole(AriaRole.Link, new() { Name = "Otwórz" }).First;
        await Expect(firstOpenBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await firstOpenBtn.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/admin/tickets/"), new PageWaitForURLOptions { Timeout = 10_000 });
        Assert.That(Page.Url, Does.Contain("returnUrl=/admin/restaurant-claims"), "URL should carry returnUrl param");

        var backBtn = Page.GetByRole(AriaRole.Link, new() { Name = "Wróc" })
            .Or(Page.GetByRole(AriaRole.Link, new() { Name = "Wroc" }))
            .Or(Page.Locator("a.btn-outline-secondary").First);
        await Expect(backBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await backBtn.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/admin/restaurant-claims"), new PageWaitForURLOptions { Timeout = 10_000 });
    }
}
