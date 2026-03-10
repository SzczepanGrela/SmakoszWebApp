using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T11_BusinessDashboardTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanAccessDashboard_AndSeeStatistics()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dashboard");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/restaurant/dashboard");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("Dashboard",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        var recenzjeCard = Page.Locator("small.text-muted", new() { HasText = "Recenzje" });
        await Expect(recenzjeCard).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var sredniaCard = Page.Locator("small.text-muted", new() { HasText = "Średnia ocena" });
        await Expect(sredniaCard).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var daniaCard = Page.Locator("small.text-muted", new() { HasText = "Dania w menu" });
        await Expect(daniaCard).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        // Use h6 text inside the cards to avoid strict mode violations with nav links
        var addDishText = Page.Locator("h6", new() { HasText = "Dodaj nowe danie" });
        await Expect(addDishText).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var reviewsText = Page.Locator("h6", new() { HasText = "Zobacz recenzje" });
        await Expect(reviewsText).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var statsText = Page.Locator("h6", new() { HasText = "Statystyki" });
        await Expect(statsText).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
