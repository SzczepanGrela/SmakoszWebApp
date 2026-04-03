using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T88_AdminNodeMonitoringTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewNodeMonitoring()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/nodes");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/nodes");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Monitoring węzłów");

        // Assert cards are visible (3 nodes from seed data)
        var nodeCards = Page.Locator(".card");
        await nodeCards.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var cardCount = await nodeCards.CountAsync();
        Assert.That(cardCount, Is.GreaterThanOrEqualTo(3),
            "Should have at least 3 node cards from seed data");

        // Assert "api-main" node has "online" badge
        var apiCard = Page.Locator(".card", new() { HasText = "api-main" });
        await Expect(apiCard).ToBeVisibleAsync();
        var onlineBadge = apiCard.Locator(".badge", new() { HasText = "online" });
        await Expect(onlineBadge).ToBeVisibleAsync();

        // Assert "gpu-worker-1" node has "offline" badge
        var gpuCard = Page.Locator(".card", new() { HasText = "gpu-worker-1" });
        await Expect(gpuCard).ToBeVisibleAsync();
        var offlineBadge = gpuCard.Locator(".badge", new() { HasText = "offline" });
        await Expect(offlineBadge).ToBeVisibleAsync();

        // Assert refresh button exists
        var refreshBtn = Page.Locator("button", new() { HasText = "Odśwież" });
        await Expect(refreshBtn).ToBeVisibleAsync();
    }
}
