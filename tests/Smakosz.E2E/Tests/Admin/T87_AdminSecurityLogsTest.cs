using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T87_AdminSecurityLogsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewAndFilterSecurityLogs()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/security");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/security");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Logi bezpieczeństwa");

        // Assert table has rows from seed (3 security log entries)
        var logRows = Page.Locator("table tbody tr");
        await logRows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var allRowCount = await logRows.CountAsync();
        Assert.That(allRowCount, Is.GreaterThanOrEqualTo(3),
            "Should have at least 3 security log entries from seed data");

        // Assert FailedLogin row has table-danger class
        var dangerRows = Page.Locator("tr.table-danger");
        var dangerCount = await dangerRows.CountAsync();
        Assert.That(dangerCount, Is.GreaterThan(0),
            "FailedLogin row should have table-danger class");

        var failedLoginFilter = Page.Locator("button", new() { HasText = "FailedLogin" });
        await failedLoginFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var filteredRowCount = await logRows.CountAsync();
        Assert.That(filteredRowCount, Is.GreaterThan(0),
            "FailedLogin filter should show at least one row");
        Assert.That(filteredRowCount, Is.LessThanOrEqualTo(allRowCount),
            "FailedLogin filter should show fewer or equal rows than all");

        var allFilter = Page.Locator("button", new() { HasText = "Wszystkie" });
        await allFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var restoredCount = await logRows.CountAsync();
        Assert.That(restoredCount, Is.GreaterThanOrEqualTo(allRowCount),
            "All filter should restore original row count");
    }
}
