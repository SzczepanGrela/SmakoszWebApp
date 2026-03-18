using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T13_RegisterValidationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Register_WithInvalidData_ShowsValidationErrors()
    {
        await NavigateAndWaitAsync("/register");

        var usernameInput = Page.Locator("input[type='text']").First;
        var emailInput = Page.Locator("input[type='email']");
        var passwordInput = Page.Locator(".input-group input[type='password']");
        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Zarejestruj się" });
        var errorAlert = Page.Locator(".alert-danger").First;

        // Bypass HTML5 validation so we can test server-side validation
        await Page.EvaluateAsync("document.querySelectorAll('form').forEach(f => f.setAttribute('novalidate', ''))");

        await WaitForTurnstileAsync();

        await usernameInput.FillAsync("ab");
        await emailInput.FillAsync("test@x.com");
        await passwordInput.FillAsync("TestHaslo123!");
        await submitButton.ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await usernameInput.ClearAsync();
        await usernameInput.FillAsync("test-ok");
        await emailInput.ClearAsync();
        await emailInput.FillAsync("test-short-pass@x.com");
        await passwordInput.ClearAsync();
        await passwordInput.FillAsync("short");
        await submitButton.ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await usernameInput.ClearAsync();
        await usernameInput.FillAsync("jan-kowalski");
        await emailInput.ClearAsync();
        await emailInput.FillAsync("new@test.com");
        await passwordInput.ClearAsync();
        await passwordInput.FillAsync("TestHaslo123!");
        await submitButton.ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var takenUsernameError = await errorAlert.TextContentAsync();
        Assert.That(takenUsernameError!.ToLower(),
            Does.Contain("zajeta").Or.Contain("zajety").Or.Contain("istnieje").Or.Contain("zajęta").Or.Contain("zajęty"),
            $"Expected 'username taken' error. Got: {takenUsernameError}");

        await usernameInput.ClearAsync();
        await usernameInput.FillAsync("new-user");
        await emailInput.ClearAsync();
        await emailInput.FillAsync(TestConstants.UserEmail);
        await passwordInput.ClearAsync();
        await passwordInput.FillAsync("TestHaslo123!");
        await submitButton.ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var takenEmailError = await errorAlert.TextContentAsync();
        Assert.That(takenEmailError!.ToLower(),
            Does.Contain("uzywany").Or.Contain("istnieje").Or.Contain("zajety").Or.Contain("używany").Or.Contain("zajęty").Or.Contain("zarejestrowany").Or.Contain("email"),
            $"Expected 'email taken' error. Got: {takenEmailError}");
    }
}
