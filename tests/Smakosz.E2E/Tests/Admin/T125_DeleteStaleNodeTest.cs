using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T125_DeleteStaleNodeTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanDeleteStaleNode()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/nodes");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("dead-test-node");

        var staleCard = Page.Locator(".card", new() { HasText = "dead-test-node" });
        await Expect(staleCard).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var deleteButton = staleCard.Locator("button", new() { HasText = "Usuń" }).First;
        await Expect(deleteButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await deleteButton.ClickAsync();

        await Page.WaitForTimeoutAsync(800);
        var confirmButton = Page.Locator("button.btn-danger", new() { HasText = "Potwierdź" }).First;

        await Expect(confirmButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await confirmButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent, Does.Not.Contain("dead-test-node"),
            "Stale node should be removed from page after delete confirmation");
    }
}
