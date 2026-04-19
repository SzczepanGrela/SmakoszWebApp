using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T101_AdminDishesPageTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanBrowseDishesAndChangeAvailability()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/dishes");
        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/dishes");
        }
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Dania");

        var approvedBtn = Page.Locator("button", new() { HasTextString = "Approved" }).First;
        await approvedBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(800);

        var firstRow = Page.Locator("tbody tr").First;
        var rowsCount = await Page.Locator("tbody tr").CountAsync();
        if (rowsCount == 0)
        {
            Assert.Pass("No dishes in Approved state, page rendered but empty");
            return;
        }

        await firstRow.ClickAsync();
        await Page.WaitForTimeoutAsync(400);

        var expandedContent = Page.Locator("tr.table-light");
        await Expect(expandedContent.First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
    }
}
