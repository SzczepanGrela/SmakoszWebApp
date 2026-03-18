using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T86_AdminAuditLogsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewAndFilterAuditLogs()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/audit-logs");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/audit-logs");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Dziennik audytu");

        var logRows = Page.Locator("table tbody tr");
        await logRows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var allRowCount = await logRows.CountAsync();
        Assert.That(allRowCount, Is.GreaterThanOrEqualTo(3),
            "Should have at least 3 audit log entries from seed data");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Czas"), Is.True, "Should show 'Czas' column");
        Assert.That(pageContent.Contains("Operacja"), Is.True, "Should show 'Operacja' column");
        Assert.That(pageContent.Contains("Tabela"), Is.True, "Should show 'Tabela' column");

        var configFilter = Page.Locator("button", new() { HasText = "config" });
        await configFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var filteredRowCount = await logRows.CountAsync();
        Assert.That(filteredRowCount, Is.GreaterThan(0), "Config filter should show at least one row");
        Assert.That(filteredRowCount, Is.LessThanOrEqualTo(allRowCount),
            "Config filter should show fewer or equal rows than all");

        var allFilter = Page.Locator("button", new() { HasText = "Wszystkie" });
        await allFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var restoredCount = await logRows.CountAsync();
        Assert.That(restoredCount, Is.GreaterThanOrEqualTo(allRowCount),
            "All filter should restore original row count");
    }
}
