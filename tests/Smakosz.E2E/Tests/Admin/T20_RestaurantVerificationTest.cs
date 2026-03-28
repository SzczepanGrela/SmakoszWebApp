using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T20_RestaurantVerificationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanVerifyUnverifiedRestaurant()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/restaurants");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/restaurants");
        }

        await WaitForBlazorLoadedAsync();

        var table = Page.Locator("table").First;
        await Expect(table).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
        await Page.WaitForTimeoutAsync(2000);

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nowa Restauracja"), Is.True,
            "Admin restaurants page should show 'Nowa Restauracja'");

        var restaurantRow = Page.Locator("tr", new() { HasText = "Nowa Restauracja" });
        await Expect(restaurantRow.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var verifyButton = restaurantRow.First.Locator("button", new() { HasText = "Zweryfikuj" });
        await Expect(verifyButton.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await verifyButton.First.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var verifyButtonAfter = restaurantRow.First.Locator("button", new() { HasText = "Zweryfikuj" });
        var verifyCount = await verifyButtonAfter.CountAsync();

        var updatedContent = await Page.ContentAsync();
        var hasVerifiedBadge = updatedContent.Contains("Zweryfikowana");

        Assert.That(verifyCount == 0 || hasVerifiedBadge, Is.True,
            "Verify button should disappear or verified badge should appear after verification");
    }
}
