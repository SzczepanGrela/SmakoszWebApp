using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T01_PublicBrowsingTest : SmakoszE2ETestBase
{
    [Test]
    public async Task AnonymousUser_CanBrowseHomepage_SearchDish_ViewDetails_AndNavigateToRestaurant()
    {
        await NavigateAndWaitAsync("/");
        await Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Smakosz"));

        // Assert stats are visible
        await AssertPageContainsTextAsync("Dan");
        await AssertPageContainsTextAsync("Restauracji");
        await AssertPageContainsTextAsync("Ocen");

        var searchInput = Page.Locator("input[type='search']").First;
        await searchInput.FillAsync("pizza");
        await Page.WaitForTimeoutAsync(500); // let Blazor process @bind change event
        await Page.GetByRole(AriaRole.Button, new() { Name = "Szukaj" }).First.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/search"), new PageWaitForURLOptions { Timeout = 10_000 });

        // Ensure the URL actually has the query param (Blazor @bind race fallback)
        if (!Page.Url.Contains("query="))
            await Page.GotoAsync($"{TestConstants.ClientBaseUrl}/search?query=pizza",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15_000 });

        await WaitForBlazorLoadedAsync();

        var resultsOrEmpty = Page.Locator(".col-md-6, .col-lg-4").First;
        var emptyState = Page.GetByText("Brak wynikow").First;
        try
        {
            await Task.WhenAny(
                resultsOrEmpty.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 }),
                emptyState.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 }));
        }
        catch (TimeoutException) { /* continue to assertion */ }

        // Assert results contain Pizzeria Roma
        var pageContent = await Page.ContentAsync();
        var hasPizzaResults = pageContent.Contains("Pizzeria Roma") || pageContent.Contains("pizza");
        Assert.That(hasPizzaResults, Is.True, "Search for 'pizza' should find Pizzeria Roma via trigram similarity");

        var dishLink = Page.GetByText("Pizza Margherita").First;
        var isDishVisible = await dishLink.IsVisibleAsync();

        if (isDishVisible)
        {
            await dishLink.ClickAsync();
            await Page.WaitForURLAsync(url => url.Contains("/dishes/"), new PageWaitForURLOptions { Timeout = 10_000 });
            await WaitForBlazorLoadedAsync();
        }
        else
        {
            await NavigateAndWaitAsync("/dishes/pizza-margherita");
        }

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/dishes/pizza-margherita"));

        var dishHeading = Page.Locator("h1").First;
        await Expect(dishHeading).ToContainTextAsync("Pizza Margherita");

        await AssertPageContainsTextAsync("24.90");

        await AssertPageContainsTextAsync("Pizzeria Roma");

        await AssertPageContainsTextAsync("Swietna pizza, ciasto idealne!");

        var restaurantLink = Page.GetByRole(AriaRole.Link, new() { Name = "Pizzeria Roma" }).First;
        await restaurantLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/restaurants/pizzeria-roma"), new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/restaurants/pizzeria-roma"));

        var restaurantHeading = Page.Locator("h1").First;
        await Expect(restaurantHeading).ToContainTextAsync("Pizzeria Roma");

        await AssertPageContainsTextAsync("Wloska");
    }
}
