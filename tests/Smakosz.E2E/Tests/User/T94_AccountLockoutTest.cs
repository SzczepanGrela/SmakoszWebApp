using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T94_AccountLockoutTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Login_RepeatedFailures_ShowsLockoutMessage()
    {
        await NavigateAndWaitAsync("/login");

        for (var i = 0; i < 5; i++)
        {
            await Page.Locator("input[type='email']").ClearAsync();
            await Page.Locator("input[type='email']").FillAsync(TestConstants.UserEmail);
            await Page.Locator(".input-group input[type='password']").ClearAsync();
            await Page.Locator(".input-group input[type='password']").FillAsync("ZleHaslo123!");
            if (i == 0)
                await WaitForTurnstileAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

            var errorAlert = Page.Locator(".alert-danger").First;
            await Expect(errorAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        }

        await Page.Locator("input[type='email']").ClearAsync();
        await Page.Locator("input[type='email']").FillAsync(TestConstants.UserEmail);
        await Page.Locator(".input-group input[type='password']").ClearAsync();
        await Page.Locator(".input-group input[type='password']").FillAsync(TestConstants.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        var lockoutAlert = Page.Locator(".alert-danger").First;
        await Expect(lockoutAlert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var lockoutText = await lockoutAlert.TextContentAsync();
        Assert.That(lockoutText!.ToLower(), Does.Contain("zablokowane").Or.Contain("zbyt wielu"),
            $"Expected lockout error message. Got: {lockoutText}");
    }
}
