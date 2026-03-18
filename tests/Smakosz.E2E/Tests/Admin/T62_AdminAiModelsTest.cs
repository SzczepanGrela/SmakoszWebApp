using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T62_AdminAiModelsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanAccessAiModelsPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/ai-models");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/ai-models");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Modele AI");

        var pageContent = await Page.ContentAsync();

        if (pageContent.Contains("Brak modeli AI"))
        {
            Assert.Pass("No AI models - empty state verified");
        }

        var modelCards = Page.Locator(".card.shadow-sm");
        var cardCount = await modelCards.CountAsync();

        if (cardCount > 0)
        {
            var hasLabels = pageContent.Contains("Przetworzone") || pageContent.Contains("Ostatnie");
            Assert.That(hasLabels, Is.True, "Model cards should contain status labels");
            Assert.Pass($"AI models page accessible - {cardCount} model card(s) found");
        }

        Assert.Pass("AI models page accessible");
    }
}
