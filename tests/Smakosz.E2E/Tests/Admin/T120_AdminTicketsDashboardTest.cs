using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T120_AdminTicketsDashboardTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_TicketsDashboard_ShowsTypeCards_NotTicketList()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/tickets");
        await WaitForBlazorLoadedAsync();

        var table = Page.Locator("table");
        Assert.That(await table.IsVisibleAsync(), Is.False, "Dashboard should not render a tickets table");

        var cards = Page.Locator(".card");
        var count = await cards.CountAsync();
        Assert.That(count, Is.GreaterThanOrEqualTo(3), "Dashboard should render at least 3 type cards");

        var firstLink = Page.GetByRole(AriaRole.Link, new() { Name = "Przejdź" }).First;
        await Expect(firstLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
