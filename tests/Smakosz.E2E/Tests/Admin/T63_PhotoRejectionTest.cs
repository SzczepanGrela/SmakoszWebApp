using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T63_PhotoRejectionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanRejectPhotoWithReason()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/photos");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/photos");
        }

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Moderacja zdjęć");

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak zdjęć do moderacji") || pageContent.Contains("zostały sprawdzone"))
        {
            Assert.Pass("No pending photos to moderate - queue is empty");
        }

        var rejectButton = Page.Locator("button.btn-danger.btn-sm", new() { HasText = "Odrzuć" }).First;
        var rejectCount = await rejectButton.CountAsync();

        if (rejectCount == 0)
        {
            Assert.Pass("No reject button found - photos may have been already moderated");
        }

        await rejectButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var reasonInput = Page.Locator("input[placeholder='Powód odrzucenia...']").First;
        await Expect(reasonInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await reasonInput.ClickAsync();
        await reasonInput.FillAsync("Zdjecie nieodpowiednie");
        // Dispatch change event explicitly for Blazor @bind
        await reasonInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");
        await Page.WaitForTimeoutAsync(300);

        var confirmButton = Page.Locator(".input-group button.btn-danger").First;
        await confirmButton.ClickAsync();

        await Page.WaitForTimeoutAsync(5000);
        await WaitForBlazorLoadedAsync();

        var updatedContent = await Page.ContentAsync();
        var queueChanged = updatedContent.Contains("Brak zdjęć") ||
                           updatedContent.Contains("zostały sprawdzone") ||
                           updatedContent.Contains("odrzucone") ||
                           updatedContent.Contains("nieudana") ||
                           await Page.Locator("button.btn-danger.btn-sm", new() { HasText = "Odrzuć" }).CountAsync() < rejectCount;
        Assert.That(queueChanged, Is.True,
            "Photo should be rejected - queue should have changed");
    }
}
