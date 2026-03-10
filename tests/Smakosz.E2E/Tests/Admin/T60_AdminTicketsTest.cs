using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T60_AdminTicketsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanBrowseTicketsPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/tickets");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/tickets");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Zgłoszenia");

        await AssertPageContainsTextAsync("Wszystkie");
        await AssertPageContainsTextAsync("Otwarte");
        await AssertPageContainsTextAsync("W toku");
        await AssertPageContainsTextAsync("Rozwiązane");

        var openFilter = Page.GetByRole(AriaRole.Button, new() { Name = "Otwarte" }).First;
        await openFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var pageContent = await Page.ContentAsync();
        var hasTypeFilters = pageContent.Contains("Kontakt") || pageContent.Contains("Recenzja") || pageContent.Contains("Zdjęcie");
        Assert.That(hasTypeFilters, Is.True, "Should display type filter buttons");

        if (pageContent.Contains("Brak zgłoszeń"))
        {
            Assert.Pass("No tickets available - empty state verified");
        }

        // Try to find a ticket card with detail link
        var detailLink = Page.Locator("a", new() { HasText = "Szczegóły" }).First;
        var detailCount = await detailLink.CountAsync();

        if (detailCount > 0)
        {
            await detailLink.ClickAsync();
            await WaitForBlazorLoadedAsync();

            // Assert redirected to ticket detail page
            Assert.That(Page.Url, Does.Contain("/admin/tickets/"),
                "Should navigate to ticket details page");
        }

        Assert.Pass("Admin tickets page accessible and functional");
    }
}
