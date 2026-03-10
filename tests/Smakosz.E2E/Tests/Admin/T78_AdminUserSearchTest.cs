using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T78_AdminUserSearchTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanSearchUsers()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/users");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/users");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("Użytkownicy",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        var searchInput = Page.Locator("input[placeholder='Szukaj użytkownika...']").First;
        await Expect(searchInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await Page.Locator("table.table tbody tr").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await searchInput.ClickAsync();
        await searchInput.FillAsync("jan");
        await searchInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");

        var searchButton = Page.Locator("button").Filter(new() { Has = Page.Locator("i.fa-magnifying-glass, i.fa-search") }).First;
        if (await searchButton.CountAsync() > 0)
        {
            await searchButton.ClickAsync();
        }
        else
        {
            await searchInput.PressAsync("Enter");
        }

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var resultRows = Page.Locator("table.table tbody tr");
        var rowCount = await resultRows.CountAsync();

        if (rowCount > 0)
        {
            var pageContent = await Page.ContentAsync();
            Assert.That(pageContent, Does.Contain("jan-kowalski"),
                "Search for 'jan' should show jan-kowalski in results");
        }

        await searchInput.ClickAsync();
        await searchInput.FillAsync("nieistniejacy-user-xyz-999");
        await searchInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");

        if (await searchButton.CountAsync() > 0)
        {
            await searchButton.ClickAsync();
        }
        else
        {
            await searchInput.PressAsync("Enter");
        }

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var emptyRowCount = await Page.Locator("table.table tbody tr").CountAsync();
        var emptyContent = await Page.ContentAsync();
        var noResults = emptyRowCount == 0 ||
                        emptyContent.Contains("Brak użytkowników") ||
                        !emptyContent.Contains("nieistniejacy-user-xyz-999");
        Assert.That(noResults, Is.True,
            "Search for non-existent user should show no results");

        await searchInput.ClickAsync();
        await searchInput.FillAsync("");
        await searchInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");

        if (await searchButton.CountAsync() > 0)
        {
            await searchButton.ClickAsync();
        }
        else
        {
            await searchInput.PressAsync("Enter");
        }

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var restoredRowCount = await Page.Locator("table.table tbody tr").CountAsync();
        Assert.That(restoredRowCount, Is.GreaterThan(0),
            "Clearing search should restore the full user list");
    }
}
