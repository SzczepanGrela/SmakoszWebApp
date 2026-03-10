using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T79_AdminRestaurantSearchTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanSearchRestaurants()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/restaurants");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/restaurants");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Restauracje");

        var searchInput = Page.Locator("input[placeholder='Szukaj restauracji...']").First;
        await Expect(searchInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await Page.Locator("table.table tbody tr").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await searchInput.ClickAsync();
        await searchInput.FillAsync("Pizzeria");
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
            Assert.That(pageContent, Does.Contain("Pizzeria Roma"),
                "Search for 'Pizzeria' should show Pizzeria Roma in results");
        }

        await searchInput.ClickAsync();
        await searchInput.FillAsync("nieistniejaca-restauracja-xyz-999");
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
                        emptyContent.Contains("Brak restauracji");
        Assert.That(noResults, Is.True,
            "Search for non-existent restaurant should show no results");

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
            "Clearing search should restore the full restaurant list");
    }
}
