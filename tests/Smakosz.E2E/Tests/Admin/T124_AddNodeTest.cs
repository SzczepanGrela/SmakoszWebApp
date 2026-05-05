using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T124_AddNodeTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanAddGpuNodeViaModal()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/nodes");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Monitoring");

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj node" }).First;
        await Expect(addButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addButton.ClickAsync();

        await Page.WaitForTimeoutAsync(500);

        var nodeIdInput = Page.Locator("input[placeholder='gpu-worker-1']").First;
        await Expect(nodeIdInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        var newNodeId = ("e2e-gpu-" + Guid.NewGuid().ToString("N")).Substring(0, 16);
        await nodeIdInput.FillAsync(newNodeId);

        var macInput = Page.Locator("input[placeholder='AA:BB:CC:DD:EE:FF']").First;
        await Expect(macInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await macInput.FillAsync("00:11:22:33:44:55");

        var gatewaySelect = Page.Locator("select").Last;
        await gatewaySelect.SelectOptionAsync("rbpi-gateway");

        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj" }).Last;
        await submitButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync(newNodeId);
    }
}
