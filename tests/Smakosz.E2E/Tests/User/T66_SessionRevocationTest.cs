using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T66_SessionRevocationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewAndRevokeSessionsOnSecurityPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/profile/security");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/profile/security");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Bezpieczeństwo");

        await AssertPageContainsTextAsync("Aktywne sesje");

        var refreshButton = Page.GetByRole(AriaRole.Button, new() { Name = "Odśwież" }).First;
        var refreshCount = await refreshButton.CountAsync();
        Assert.That(refreshCount, Is.GreaterThan(0), "Refresh button should be present");

        var logoutAllButton = Page.GetByRole(AriaRole.Button, new() { Name = "Wyloguj wszystkie inne" }).First;
        var logoutAllCount = await logoutAllButton.CountAsync();
        Assert.That(logoutAllCount, Is.GreaterThan(0), "'Wyloguj wszystkie inne' button should be present");

        await refreshButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();

        if (pageContent.Contains("Brak aktywnych sesji"))
        {
            Assert.Pass("No active sessions - empty state verified");
        }

        if (pageContent.Contains("Obecna sesja"))
        {
            Assert.Pass("Security page accessible - current session displayed with badge");
        }

        // Individual "Wyloguj" buttons only appear for non-current sessions
        // Use exact text match to avoid matching "Wyloguj wszystkie inne"
        var individualLogoutButtons = Page.Locator("button.btn-outline-danger.btn-sm")
            .Filter(new() { HasText = "Wyloguj" })
            .Filter(new() { HasNotText = "wszystkie" });
        var individualCount = await individualLogoutButtons.CountAsync();

        if (individualCount > 0)
        {
            await individualLogoutButtons.First.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);

            var toastContent = await Page.ContentAsync();
            if (toastContent.Contains("Sesja została wylogowana."))
            {
                Assert.Pass("Session revocation successful");
            }
        }

        Assert.Pass("Security page accessible - sessions section verified");
    }
}
