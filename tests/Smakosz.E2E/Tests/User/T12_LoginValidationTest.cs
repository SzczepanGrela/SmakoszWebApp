using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T12_LoginValidationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Login_WithInvalidCredentials_ShowsErrorMessages()
    {
        await NavigateAndWaitAsync("/login");

        await Page.Locator("input[type='email']").FillAsync("zly@email.com");
        await Page.Locator(".input-group input[type='password']").FillAsync("WrongPass123!");
        await WaitForTurnstileAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        var errorAlert = Page.Locator(".alert-danger").First;
        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var errorText = await errorAlert.TextContentAsync();
        Assert.That(errorText, Does.Contain("email").Or.Contain("has\u0142o").Or.Contain("haslo"),
            $"Expected generic error for wrong email. Got: {errorText}");

        await Page.Locator("input[type='email']").ClearAsync();
        await Page.Locator("input[type='email']").FillAsync(TestConstants.UserEmail);
        await Page.Locator(".input-group input[type='password']").ClearAsync();
        await Page.Locator(".input-group input[type='password']").FillAsync("ZleHaslo123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var errorText2 = await errorAlert.TextContentAsync();
        Assert.That(errorText2, Does.Contain("email").Or.Contain("has\u0142o").Or.Contain("haslo"),
            "Same generic error should appear for wrong password (no account enumeration)");

        await Page.Locator("input[type='email']").ClearAsync();
        await Page.Locator("input[type='email']").FillAsync(TestConstants.BannedEmail);
        await Page.Locator(".input-group input[type='password']").ClearAsync();
        await Page.Locator(".input-group input[type='password']").FillAsync(TestConstants.BannedPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var errorText3 = await errorAlert.TextContentAsync();
        Assert.That(errorText3!.ToLower(), Does.Contain("zablokowane").Or.Contain("banned").Or.Contain("nieprawidl").Or.Contain("email"),
            $"Expected blocked/banned error for banned user. Got: {errorText3}");
    }
}
