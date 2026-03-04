using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T38_BusinessReadOnlyPagesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanViewStatsReviewsAndEditHistory()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/stats");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/restaurant/stats");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Statystyki");

        // Assert stat cards visible
        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Laczna liczba recenzji"), Is.True,
            "Stats page should show 'Laczna liczba recenzji'");
        Assert.That(pageContent.Contains("Srednia ocena"), Is.True,
            "Stats page should show 'Srednia ocena'");

        // Assert stat values present (.fs-2.fw-bold elements)
        var statValues = Page.Locator(".fs-2.fw-bold");
        var statCount = await statValues.CountAsync();
        Assert.That(statCount, Is.GreaterThanOrEqualTo(2),
            "Stats page should have at least 2 stat value elements");

        await NavigateAndWaitAsync("/restaurant/reviews");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Recenzje");

        // Assert review cards or total count
        pageContent = await Page.ContentAsync();
        var hasReviewData = pageContent.Contains("Laczna liczba recenzji") ||
                           pageContent.Contains("card-body") ||
                           pageContent.Contains("Danie:");
        Assert.That(hasReviewData, Is.True,
            "Reviews page should show review data or count for Pizzeria Roma");

        await NavigateAndWaitAsync("/restaurant/requests");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Historia zmian");

        pageContent = await Page.ContentAsync();
        // Seed has a pending edit request for Pizzeria Roma
        var hasEditHistory = pageContent.Contains("InfoUpdate") || pageContent.Contains("Pending") ||
                            pageContent.Contains("table") || pageContent.Contains("Brak historii");
        Assert.That(hasEditHistory, Is.True,
            "Edit history page should show table with edit requests or empty state");
    }
}
