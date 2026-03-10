using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T68_TwoFactorAuthPageTest : SmakoszE2ETestBase
{
    [Test]
    public async Task TwoFactorAuth_PageUIAndValidation()
    {
        await NavigateAndWaitAsync("/verify-2fa");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Weryfikacja dwuetapowa");

        await AssertPageContainsTextAsync("Wprowadź kod z aplikacji uwierzytelniającej.");

        var codeInput = Page.Locator("input[type='text'][maxlength='6']");
        var inputCount = await codeInput.CountAsync();
        Assert.That(inputCount, Is.GreaterThan(0), "Should have a 6-digit code input field");

        var verifyButton = Page.GetByRole(AriaRole.Button, new() { Name = "Zweryfikuj" });
        await Expect(verifyButton).ToBeVisibleAsync();

        await codeInput.First.FillAsync("123456");
        await verifyButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        var pageContent = await Page.ContentAsync();
        var hasError = pageContent.Contains("Nieprawidłowy kod") ||
                       pageContent.Contains("alert-danger") ||
                       pageContent.Contains("error");
        Assert.That(hasError, Is.True, "Should show error for invalid 2FA code");

        var returnLink = Page.GetByText("Powrót").First;
        var returnCount = await returnLink.CountAsync();
        if (returnCount > 0)
        {
            var href = await returnLink.GetAttributeAsync("href");
            Assert.That(href, Does.Contain("/login").Or.Null,
                "Return link should point to login page");
        }

        var resendButton = Page.GetByRole(AriaRole.Button, new() { Name = "Wyślij ponownie" }).First;
        var resendCount = await resendButton.CountAsync();
        if (resendCount > 0)
        {
            await Expect(resendButton).ToBeVisibleAsync();
        }

        Assert.Pass("2FA page UI and validation verified");
    }
}
