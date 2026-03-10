using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T32_ContactFormTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanSubmitContactForm()
    {
        await NavigateAndWaitAsync("/contact");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Kontakt");

        // Fill the contact form
        var nameInput = Page.Locator("input.form-control").First;
        await nameInput.FillAsync("Test E2E");

        var emailInput = Page.Locator("input[type='email'].form-control").First;
        if (!await emailInput.IsVisibleAsync())
            emailInput = Page.Locator("input.form-control").Nth(1);
        await emailInput.FillAsync("e2e@test.com");

        // Subject field
        var subjectInput = Page.Locator("input.form-control").Nth(2);
        if (await subjectInput.IsVisibleAsync())
            await subjectInput.FillAsync("Testowy temat");

        // Message textarea
        var messageArea = Page.Locator("textarea.form-control").First;
        await messageArea.FillAsync("Wiadomosc testowa z E2E.");

        var submitButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Wyślij") }).First;
        await submitButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);

        // Assert success
        var pageContent = await Page.ContentAsync();
        Assert.That(
            pageContent.Contains("wysłana") || pageContent.Contains("Wysłana") ||
            pageContent.Contains("dziękujemy") || pageContent.Contains("Dziękujemy") ||
            pageContent.Contains("success") || pageContent.Contains("Sukces"),
            Is.True,
            "Contact form submission should show success feedback");
    }
}
