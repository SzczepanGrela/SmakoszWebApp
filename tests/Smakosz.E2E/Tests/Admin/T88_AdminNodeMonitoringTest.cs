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

        await AssertPageContainsTextAsync("Monitoring węzłów");

        var nodeCards = Page.Locator(".card");
        await nodeCards.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var cardCount = await nodeCards.CountAsync();
        Assert.That(cardCount, Is.GreaterThanOrEqualTo(3),
            "Should have at least 3 node cards from seed data");

        var apiCard = Page.Locator(".card", new() { HasText = "api-main" });
        await Expect(apiCard).ToBeVisibleAsync();
        var onlineBadge = apiCard.Locator(".badge", new() { HasText = "online" });
        await Expect(onlineBadge).ToBeVisibleAsync();

        var gpuCard = Page.Locator(".card", new() { HasText = "gpu-worker-1" });
        await Expect(gpuCard).ToBeVisibleAsync();
        var offlineBadge = gpuCard.Locator(".badge", new() { HasText = "offline" });
        await Expect(offlineBadge).ToBeVisibleAsync();

        var refreshBtn = Page.Locator("button", new() { HasText = "Odśwież" });
        await Expect(refreshBtn).ToBeVisibleAsync();
    }
}
