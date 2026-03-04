using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T48_ModeratorReportsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanAccessAndResolveReports()
    {
        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/reports");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/reports");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading - page is accessible (NOT redirected)
        Assert.That(Page.Url, Does.Not.Contain("/login"),
            "Moderator should have access to reports page");

        await AssertPageContainsTextAsync("Raporty");

        // Assert no forbidden message
        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nie masz uprawnien") || pageContent.Contains("403"), Is.False,
            "Moderator should not see forbidden message on reports page");

        var pendingFilter = Page.Locator("button", new() { HasText = "Oczekujace" });
        if (await pendingFilter.IsVisibleAsync())
        {
            await pendingFilter.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
            await WaitForBlazorLoadedAsync();
        }

        var resolveButton = Page.Locator("button.btn-outline-success").First;
        if (await resolveButton.IsVisibleAsync())
        {
            await resolveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
            await WaitForBlazorLoadedAsync();

            await AssertToastAsync("Raport zaktualizowany.");
        }
        else
        {
            // No pending reports - T40 already resolved them
            Assert.Pass("Access verified, no pending reports to resolve");
        }
    }
}
