using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T46_ModeratorPhotoModerationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanAccessPhotoModerationPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/photos");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/photos");
        }

        await WaitForBlazorLoadedAsync();

        Assert.That(Page.Url, Does.Not.Contain("/login"),
            "Moderator should have access to photo moderation page");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nie masz uprawnień") || pageContent.Contains("403"), Is.False,
            "Moderator should not see forbidden message on photo moderation");

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        var approveCount = await approveButton.CountAsync();

        if (approveCount > 0)
        {
            await approveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
            await WaitForBlazorLoadedAsync();

            var contentAfterApprove = await Page.ContentAsync();
            Assert.That(
                contentAfterApprove.Contains("zatwierdzone") || contentAfterApprove.Contains("Zatwierdźone") ||
                contentAfterApprove.Contains("Brak zdjęć"),
                Is.True,
                "Photo should be approved or queue should be empty after approval");
        }
        else
        {
            // Queue already empty (T22 approved the seed photo)
            Assert.Pass("Queue already empty - verified Moderator access to photo moderation");
        }
    }
}
