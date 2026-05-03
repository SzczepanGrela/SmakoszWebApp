using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T104_GpuWakeButtonTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanTriggerGpuWakeFromNodesPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/nodes");
        await WaitForBlazorLoadedAsync();

        var gpuCard = Page.Locator(".card", new() { HasText = "gpu-worker-1" });
        await Expect(gpuCard).ToBeVisibleAsync();

        var wakeButton = gpuCard.Locator("button.btn-warning", new() { HasText = "Obudź GPU" }).First;
        await Expect(wakeButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await Expect(wakeButton).ToBeEnabledAsync();

        await wakeButton.ClickAsync();

        // RPi gateway is unreachable in E2E so the API will respond with GatewayFailed.
        // Either an info toast (Sent) or an error toast (Błąd bramy) is acceptable - what
        // we are verifying is that the button is wired to the endpoint and the UI reacts.
        var anyToast = Page.Locator(".toast, .alert").First;
        await Expect(anyToast).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
