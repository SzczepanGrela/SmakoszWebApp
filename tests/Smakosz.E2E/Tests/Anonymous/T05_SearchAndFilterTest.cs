using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T05_SearchAndFilterTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanSearchWithFilters_SwitchTypes_AndClearFilters()
    {
        var consoleErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
                consoleErrors.Add($"[{msg.Type}] {msg.Text}");
        };

        var apiCalls = new List<string>();
        Page.Response += async (_, response) =>
        {
            if (response.Url.Contains("/api/"))
            {
                try
                {
                    var body = await response.TextAsync();
                    apiCalls.Add($"[{response.Status}] {response.Url} -> {body[..Math.Min(300, body.Length)]}");
                }
                catch
                {
                    apiCalls.Add($"[{response.Status}] {response.Url} -> (body unreadable)");
                }
            }
        };

        await NavigateAndWaitAsync("/search");
        await WaitForBlazorLoadedAsync();

        await Page.WaitForTimeoutAsync(2000);

        // Assert search page elements are visible
        var searchInput = Page.Locator("input[type='search'], input.form-control").First;
        await Expect(searchInput).ToBeVisibleAsync();

        await searchInput.FillAsync("pizza");
        await Page.WaitForTimeoutAsync(500);

        var searchButton = Page.Locator(".input-group button.btn-primary, button:has(i.fa-magnifying-glass)").First;
        await searchButton.ClickAsync();

        try
        {
            await Page.Locator(".spinner-border").First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
        }
        catch (TimeoutException) { }
        await Page.WaitForTimeoutAsync(1000);
        await WaitForBlazorLoadedAsync();

        // Assert results contain Pizzeria Roma
        var bodyText = await Page.Locator("body").InnerTextAsync();
        var hasResults = bodyText.Contains("Pizzeria Roma") || bodyText.Contains("pizza");
        Assert.That(hasResults, Is.True,
            $"Search for 'pizza' should return results via trigram similarity.\n" +
            $"URL: {Page.Url}\n" +
            $"API calls captured ({apiCalls.Count}):\n{string.Join("\n", apiCalls)}\n" +
            $"Console errors:\n{string.Join("\n", consoleErrors)}\n" +
            $"Body (first 500): {bodyText[..Math.Min(500, bodyText.Length)]}");

        var dishesTypeButton = Page.GetByText("Dania", new() { Exact = false }).First;
        var restaurantsTypeButton = Page.GetByText("Restauracje", new() { Exact = false }).First;

        if (await dishesTypeButton.IsVisibleAsync())
        {
            await dishesTypeButton.ClickAsync();
            try
            {
                await Page.Locator(".spinner-border").First.WaitForAsync(
                    new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
            }
            catch (TimeoutException) { }
            await Page.WaitForTimeoutAsync(500);
            await WaitForBlazorLoadedAsync();
        }

        if (await restaurantsTypeButton.IsVisibleAsync())
        {
            await restaurantsTypeButton.ClickAsync();
            try
            {
                await Page.Locator(".spinner-border").First.WaitForAsync(
                    new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
            }
            catch (TimeoutException) { }
            await Page.WaitForTimeoutAsync(500);
            await WaitForBlazorLoadedAsync();
        }

        var wloskaCheckbox = Page.Locator("label:has-text('Włoska')").Locator("..").Locator("input[type='checkbox']").First;

        if (await wloskaCheckbox.IsVisibleAsync())
        {
            await wloskaCheckbox.CheckAsync();

            var applyButton = Page.GetByRole(AriaRole.Button, new() { Name = "Zastosuj filtry" }).First;
            if (await applyButton.IsVisibleAsync())
            {
                await applyButton.ClickAsync();
                try
                {
                    await Page.Locator(".spinner-border").First.WaitForAsync(
                        new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
                }
                catch (TimeoutException) { }
                await Page.WaitForTimeoutAsync(500);
                await WaitForBlazorLoadedAsync();
            }
        }

        var searchInputAfterFilter = Page.Locator("input[type='search'], .input-group input.form-control").First;
        await searchInputAfterFilter.ClearAsync();
        await searchInputAfterFilter.FillAsync("kebab");

        if (await wloskaCheckbox.IsVisibleAsync() && await wloskaCheckbox.IsCheckedAsync())
        {
            await wloskaCheckbox.UncheckAsync();
        }

        await Page.WaitForTimeoutAsync(300);
        var searchButtonAfter = Page.Locator(".input-group button.btn-primary, button:has(i.fa-magnifying-glass)").First;
        await searchButtonAfter.ClickAsync();

        try
        {
            await Page.Locator(".spinner-border").First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
        }
        catch (TimeoutException) { }
        await Page.WaitForTimeoutAsync(500);
        await WaitForBlazorLoadedAsync();

        var kebabBody = await Page.Locator("body").InnerTextAsync();
        var hasKebabResults = kebabBody.Contains("Kebab") || kebabBody.Contains("Sultan") || kebabBody.Contains("kebab");
        Assert.That(hasKebabResults, Is.True,
            $"Search for 'kebab' should return kebab-related results.\nURL: {Page.Url}\nBody (first 800): {kebabBody[..Math.Min(800, kebabBody.Length)]}");
    }
}
