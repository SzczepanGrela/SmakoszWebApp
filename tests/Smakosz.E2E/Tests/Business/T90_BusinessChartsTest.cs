using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T90_BusinessChartsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanSeeCharts_OnStatisticsPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/stats");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/restaurant/stats");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("Statystyki",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        var chartTrend = Page.Locator("[data-testid='chart-review-trend']");
        await Expect(chartTrend).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        var chartRating = Page.Locator("[data-testid='chart-rating-distribution']");
        await Expect(chartRating).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var chartCategory = Page.Locator("[data-testid='chart-category-averages']");
        await Expect(chartCategory).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var chartDishes = Page.Locator("[data-testid='chart-top-dishes']");
        await Expect(chartDishes).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
