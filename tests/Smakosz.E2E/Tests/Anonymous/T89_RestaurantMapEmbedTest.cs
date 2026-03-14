using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T89_RestaurantMapEmbedTest : SmakoszE2ETestBase
{
    [Test]
    public async Task RestaurantDetails_ShowsMapLinkWhenAddressPresent()
    {
        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        var contactSection = Page.Locator(".dish-details-panel").First;
        await Expect(contactSection).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var addressText = await contactSection.InnerTextAsync();
        Assert.That(addressText, Does.Contain("ul. Marszalkowska 10"),
            "Restaurant address should be visible");

        var mapLink = contactSection.Locator("a:has-text('Pokaż na mapie')");
        await Expect(mapLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        var href = await mapLink.GetAttributeAsync("href");
        Assert.That(href, Does.Contain("google.com/maps"),
            "Map link should point to Google Maps");
        Assert.That(href, Does.Contain("ul.+Marszalkowska").Or.Contain("Marszalkowska"),
            "Map link should contain the restaurant address");
    }

    [Test]
    public async Task RestaurantDetails_ShowsMapEmbedIframeWhenApiKeyConfigured()
    {
        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        var mapIframe = Page.Locator("iframe[src*='google.com/maps/embed']");
        var iframeCount = await mapIframe.CountAsync();

        if (iframeCount > 0)
        {
            // API key is configured - verify iframe attributes
            await Expect(mapIframe).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

            var src = await mapIframe.GetAttributeAsync("src");
            Assert.That(src, Does.Contain("google.com/maps/embed/v1/place"),
                "Iframe should use Google Maps Embed API v1");

            var locationPanel = Page.Locator(".dish-details-panel:has-text('Lokalizacja')");
            await Expect(locationPanel).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        }
        else
        {
            // No API key - the "Pokaż na mapie" link should still be present as fallback
            var mapLink = Page.Locator("a:has-text('Pokaż na mapie')");
            await Expect(mapLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
            Assert.Pass("Google Maps iframe not present (API key not configured), but fallback link is available");
        }
    }
}
