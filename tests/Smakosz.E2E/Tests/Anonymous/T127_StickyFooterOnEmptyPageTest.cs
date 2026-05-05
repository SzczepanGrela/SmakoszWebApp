using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T127_StickyFooterOnEmptyPageTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Footer_StaysAtViewportBottom_OnNotFoundPage()
    {
        await NavigateAndWaitAsync("/test-404-foobar");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("404");

        var footer = Page.Locator("footer.footer").First;
        await Expect(footer).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        var footerBox = await footer.BoundingBoxAsync();
        var viewport = Page.ViewportSize;

        Assert.That(footerBox, Is.Not.Null, "Footer bounding box should be measurable");
        Assert.That(viewport, Is.Not.Null, "Viewport size should be measurable");

        var footerBottom = footerBox!.Y + footerBox.Height;
        Assert.That(footerBottom, Is.EqualTo((double)viewport!.Height).Within(2),
            $"Footer bottom edge ({footerBottom}px) should sit at viewport bottom ({viewport.Height}px) " +
            $"on a near-empty page (sticky footer pattern). Tolerance 2px for sub-pixel rounding.");
    }
}
