using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T44_AdminSystemLogsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewAndFilterSystemLogs()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/logs");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/logs");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Logi systemowe");

        // Assert table has rows from seed (3 log entries)
        var logRows = Page.Locator("table tbody tr");
        await logRows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var allRowCount = await logRows.CountAsync();
        Assert.That(allRowCount, Is.GreaterThanOrEqualTo(3),
            "Should have at least 3 log entries from seed data");

        // Assert columns visible: Czas, Poziom, Wiadomosc, Zrodlo
        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Czas"), Is.True, "Should show 'Czas' column");
        Assert.That(pageContent.Contains("Poziom"), Is.True, "Should show 'Poziom' column");
        Assert.That(pageContent.Contains("Wiadomosc"), Is.True, "Should show 'Wiadomosc' column");
        Assert.That(pageContent.Contains("Zrodlo"), Is.True, "Should show 'Zrodlo' column");

        var errorFilter = Page.Locator("button", new() { HasText = "Error" });
        await errorFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var errorRowCount = await logRows.CountAsync();

        if (errorRowCount > 0)
        {
            var dangerRows = Page.Locator("tr.table-danger");
            var dangerCount = await dangerRows.CountAsync();
            Assert.That(dangerCount, Is.GreaterThan(0),
                "Error filter should show rows with table-danger class");
            Assert.That(errorRowCount, Is.LessThanOrEqualTo(allRowCount),
                "Error filter should show fewer or equal rows than all");
        }

        var allFilter = Page.Locator("button", new() { HasText = "Wszystkie" });
        await allFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var restoredCount = await logRows.CountAsync();
        Assert.That(restoredCount, Is.GreaterThanOrEqualTo(allRowCount),
            "All filter should restore original row count");
    }
}
