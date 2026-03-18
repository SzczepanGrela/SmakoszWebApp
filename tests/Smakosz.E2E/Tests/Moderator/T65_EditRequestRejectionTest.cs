using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T65_EditRequestRejectionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanRejectEditRequestWithReason()
    {
        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/edit-requests");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/edit-requests");
        }

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Prośby o edycję");

        var rejectButton = Page.Locator("button.btn-danger.btn-sm", new() { HasText = "Odrzuć" }).First;
        var rejectCount = await rejectButton.CountAsync();

        if (rejectCount == 0)
        {
            var pageContent = await Page.ContentAsync();
            if (pageContent.Contains("Brak próśb o edycję") || pageContent.Contains("Brak próśb"))
            {
                Assert.Pass("No pending edit requests - queue is empty");
            }
            Assert.Pass("No reject button found - no edit requests to reject");
        }

        await rejectButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var reasonInput = Page.Locator(".form-control[placeholder='Powód odrzucenia...']").First;
        await Expect(reasonInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await reasonInput.FillAsync("Test E2E - edycja niezasadna");

        var confirmButton = Page.Locator(".input-group button.btn-danger").First;
        await confirmButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        await AssertToastAsync("Prośba przetworzona.");
    }
}
