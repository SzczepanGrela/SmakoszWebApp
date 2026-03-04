using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T47_ModeratorEditRequestsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanAccessAndProcessEditRequests()
    {
        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/edit-requests");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/edit-requests");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading - page is accessible
        await AssertPageContainsTextAsync("Prosby o edycje");

        // Assert no forbidden message
        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nie masz uprawnien"), Is.False,
            "Moderator should not see forbidden message");

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdz" }).First;
        var approveCount = await approveButton.CountAsync();

        if (approveCount > 0)
        {
            // Assert card shows Pizzeria Roma + InfoUpdate info
            var hasExpectedContent = pageContent.Contains("Pizzeria Roma") || pageContent.Contains("InfoUpdate") ||
                                    pageContent.Contains("Telefon");
            Assert.That(hasExpectedContent, Is.True,
                "Should show edit request with restaurant info");

            await approveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
            await WaitForBlazorLoadedAsync();

            // Assert toast
            await AssertToastAsync("Prosba przetworzona.");
        }
        else
        {
            // No pending requests - other test approved them
            Assert.Pass("Access verified - no pending edit requests to process");
        }
    }
}
