using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T72_RecommendationsContentTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewRecommendationsPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/recommendations");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/recommendations");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Rekomendacje dla Ciebie");

        await AssertPageContainsTextAsync("Dania dobrane specjalnie na podstawie Twoich preferencji i recenzji.");

        var pageContent = await Page.ContentAsync();

        if (pageContent.Contains("Brak rekomendacji"))
        {
            var addReviewsLink = Page.GetByText("Dodaj recenzje").First;
            var linkCount = await addReviewsLink.CountAsync();
            Assert.That(linkCount, Is.GreaterThan(0),
                "Empty state should show 'Dodaj recenzje' link");
            Assert.Pass("No recommendations - empty state with action link verified");
        }

        if (pageContent.Contains("dopasowanie"))
        {
            var matchBadge = Page.Locator(".badge.bg-primary", new() { HasText = "dopasowanie" }).First;
            var badgeCount = await matchBadge.CountAsync();
            Assert.That(badgeCount, Is.GreaterThan(0), "Should show match percentage badge");
        }

        var aiIcon = Page.Locator("i.fa-solid.fa-robot");
        var iconCount = await aiIcon.CountAsync();

        Assert.Pass("Recommendations page content verified");
    }
}
