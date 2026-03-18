using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T26_AnonymousPagesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Anonymous_CanViewPublicProfileContactAboutAnd404()
    {
        await NavigateAndWaitAsync("/users/jan-kowalski");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("jan-kowalski");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Recenzj") || pageContent.Contains("recenzj"), Is.True,
            "User profile should show review count info");

        // jan has approved reviews in seed
        var reviewCards = Page.Locator(".card, .review-card, [class*='review']");
        var reviewCount = await reviewCards.CountAsync();
        Assert.That(reviewCount, Is.GreaterThan(0),
            "Jan-kowalski should have visible review cards on profile");

        await NavigateAndWaitAsync("/contact");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Kontakt");

        var formInputs = Page.Locator("input.form-control, textarea.form-control");
        var inputCount = await formInputs.CountAsync();
        Assert.That(inputCount, Is.GreaterThanOrEqualTo(3),
            "Contact page should have form fields (name, email, subject, message)");

        await NavigateAndWaitAsync("/about");
        await WaitForBlazorLoadedAsync();

        pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("O nas") || pageContent.Contains("Smakosz"), Is.True,
            "About page should contain 'O nas' or 'Smakosz'");

        await NavigateAndWaitAsync("/nonexistent-page-xyz");
        await WaitForBlazorLoadedAsync();

        pageContent = await Page.ContentAsync();
        Assert.That(
            pageContent.Contains("404") || pageContent.Contains("nie znaleziono") || pageContent.Contains("Nie znaleziono"),
            Is.True,
            "Nonexistent page should show 404 or 'nie znaleziono'");
    }
}
