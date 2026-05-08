using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T88_AdminNodeMonitoringTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewNodeStripOnJobsPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/jobs");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/jobs");
        }

        await WaitForBlazorLoadedAsync();

        var pills = Page.Locator(".node-pill-wrap");
        await pills.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var pillCount = await pills.CountAsync();
        Assert.That(pillCount, Is.EqualTo(3),
            "Strip should render exactly three pills (api + rbpi + gpu)");

        await Expect(Page.Locator(".node-pill-wrap", new() { HasText = "vps-hetzner-prod" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".node-pill-wrap", new() { HasText = "rbpi-gateway" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".node-pill-wrap", new() { HasText = "gpu-homelab" })).ToBeVisibleAsync();
    }
}
