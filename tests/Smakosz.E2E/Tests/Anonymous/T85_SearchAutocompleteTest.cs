using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T85_SearchAutocompleteTest : SmakoszE2ETestBase
{
    [Test]
    public async Task TypingInSearch_ShowsSuggestions_ClickNavigates()
    {
        await NavigateAndWaitAsync("/");
        await WaitForBlazorLoadedAsync();

        var searchInput = Page.Locator("input[type='search']").First;
        await Expect(searchInput).ToBeVisibleAsync();

        await searchInput.FillAsync("pizza");
        await Page.WaitForTimeoutAsync(500);

        var dropdown = Page.Locator(".autocomplete-dropdown");
        await Expect(dropdown).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var items = Page.Locator(".autocomplete-item");
        var itemCount = await items.CountAsync();
        Assert.That(itemCount, Is.GreaterThan(0), "Autocomplete should show suggestions for 'pizza'");

        await items.First.ClickAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dishes/") || url.Contains("/restaurants/"),
            new PageWaitForURLOptions { Timeout = 10_000 });

        await WaitForBlazorLoadedAsync();
    }

    [Test]
    public async Task EscapeKey_ClosesDropdown()
    {
        await NavigateAndWaitAsync("/");
        await WaitForBlazorLoadedAsync();

        var searchInput = Page.Locator("input[type='search']").First;
        await searchInput.FillAsync("pizza");
        await Page.WaitForTimeoutAsync(500);

        var dropdown = Page.Locator(".autocomplete-dropdown");
        await Expect(dropdown).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await Page.Keyboard.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(200);

        await Expect(dropdown).ToBeHiddenAsync(
            new LocatorAssertionsToBeHiddenOptions { Timeout = 5_000 });
    }
}
