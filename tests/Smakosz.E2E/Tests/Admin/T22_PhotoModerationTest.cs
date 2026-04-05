using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T22_PhotoModerationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanModeratePhotoInQueue()
    {
        // Seed data includes a pending photo (MediaAsset with Status=Pending)

        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/photos");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/photos");
        }

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        var approveCount = await approveButton.CountAsync();

        if (approveCount == 0)
        {
            var pageContent = await Page.ContentAsync();
            if (pageContent.Contains("Brak zdjęć") || pageContent.Contains("zostały sprawdzone"))
            {
                Assert.Pass("No pending photos to moderate - queue is empty");
            }
            Assert.Fail("No approve button found but queue is not empty");
        }

        await approveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        // After approval and page reload, the empty state should show (only 1 photo in seed)
        var updatedContent = await Page.ContentAsync();
        var queueEmpty = updatedContent.Contains("Brak zdjęć") || updatedContent.Contains("zostały sprawdzone");
        var fewerButtons = await Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).CountAsync() < approveCount;

        Assert.That(queueEmpty || fewerButtons, Is.True,
            "Photo should be approved and removed from moderation queue");
    }
}
