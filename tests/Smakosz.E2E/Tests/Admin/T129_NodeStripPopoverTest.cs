using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T129_NodeStripPopoverTest : SmakoszE2ETestBase
{
    [Test]
    public async Task ClickingGpuPill_OpensPopoverWithGpuDetail()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/jobs");
        await WaitForBlazorLoadedAsync();

        var gpuPill = Page.Locator(".node-pill-wrap", new() { HasText = "gpu-homelab" });
        await Expect(gpuPill).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await Expect(gpuPill.Locator(".node-pill-popover")).Not.ToBeVisibleAsync();

        var pillButton = gpuPill.Locator("button.btn-outline-secondary").First;
        await pillButton.ClickAsync();

        var popover = gpuPill.Locator(".node-pill-popover");
        await Expect(popover).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await Expect(popover).ToContainTextAsync("GTX 1060");
        await Expect(popover).ToContainTextAsync("homelab");
    }
}
