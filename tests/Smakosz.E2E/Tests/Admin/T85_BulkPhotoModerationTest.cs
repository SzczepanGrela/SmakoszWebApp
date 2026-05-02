using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T85_BulkPhotoModerationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanBulkApprovePhotosInQueue()
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

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak zdjęć") || pageContent.Contains("zostały sprawdzone"))
        {
            Assert.Pass("No pending photos to bulk moderate - queue is empty");
        }

        var selectAllCheckbox = Page.Locator("#select-all-page");
        await selectAllCheckbox.WaitForAsync(new() { Timeout = 5000 });
        await selectAllCheckbox.CheckAsync();

        await Page.WaitForTimeoutAsync(500);

        var bulkApproveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź zaznaczone" });
        await bulkApproveButton.WaitForAsync(new() { Timeout = 5000 });

        var initialCardCount = await Page.Locator(".card.shadow-sm").CountAsync();

        await bulkApproveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var updatedContent = await Page.ContentAsync();
        var queueEmpty = updatedContent.Contains("Brak zdjęć") || updatedContent.Contains("zostały sprawdzone");
        var fewerCards = await Page.Locator(".card.shadow-sm").CountAsync() < initialCardCount;

        Assert.That(queueEmpty || fewerCards, Is.True,
            "Bulk approve should clear the moderation queue or shrink it");
    }
}
