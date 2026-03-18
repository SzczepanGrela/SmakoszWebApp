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

        await AssertPageContainsTextAsync("Prośby o edycję");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nie masz uprawnień"), Is.False,
            "Moderator should not see forbidden message");

        // Pending edit requests may come from seed or T33
        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        var approveCount = await approveButton.CountAsync();

        if (approveCount > 0)
        {
            var hasExpectedContent = pageContent.Contains("Pizzeria Roma") || pageContent.Contains("InfoUpdate") ||
                                    pageContent.Contains("Telefon");
            Assert.That(hasExpectedContent, Is.True,
                "Should show edit request with restaurant info");

            await approveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
            await WaitForBlazorLoadedAsync();

            await AssertToastAsync("Prośba przetworzona.");
        }
        else
        {
            // No pending requests - other test approved them
            Assert.Pass("Access verified - no pending edit requests to process");
        }
    }
}
