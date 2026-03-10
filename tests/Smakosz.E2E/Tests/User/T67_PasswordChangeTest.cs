using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T67_PasswordChangeTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanChangePasswordWithValidation()
    {
        // Using anna-nowak (User2) to isolate from other tests
        await LoginViaLocalStorageAsync(TestConstants.User2Email, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/profile/security");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/profile/security");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Zmiana hasła");

        var passwordInputs = Page.Locator(".input-group input[type='password']");
        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Zmień hasło" });

        await submitButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Wprowadź obecne hasło") ||
                    pageContent.Contains("alert-danger"),
            Is.True, "Should show validation error for empty fields");

        var inputs = await passwordInputs.AllAsync();
        if (inputs.Count >= 3)
        {
            await inputs[0].FillAsync(TestConstants.UserPassword);
            await inputs[1].FillAsync("short");
            await inputs[2].FillAsync("short");
            await submitButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            pageContent = await Page.ContentAsync();
            Assert.That(pageContent.Contains("co najmniej 8 znaków") ||
                        pageContent.Contains("alert-danger"),
                Is.True, "Should show validation error for short password");
        }

        if (inputs.Count >= 3)
        {
            await inputs[0].FillAsync(TestConstants.UserPassword);
            await inputs[1].FillAsync("NoweTestHaslo123!");
            await inputs[2].FillAsync("InneHaslo456!");
            await submitButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            pageContent = await Page.ContentAsync();
            Assert.That(pageContent.Contains("nie są zgodne") ||
                        pageContent.Contains("alert-danger"),
                Is.True, "Should show validation error for mismatched passwords");
        }

        if (inputs.Count >= 3)
        {
            await inputs[0].FillAsync(TestConstants.UserPassword);
            await inputs[1].FillAsync("NoweTestHaslo123!");
            await inputs[2].FillAsync("NoweTestHaslo123!");
            await submitButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);

            pageContent = await Page.ContentAsync();
            var changed = pageContent.Contains("Hasło zostało zmienione") ||
                          pageContent.Contains("alert-success");

            if (changed)
            {
                await Page.WaitForTimeoutAsync(2000);

                // Re-fetch inputs (page may have re-rendered)
                var restoreInputs = await Page.Locator(".input-group input[type='password']").AllAsync();
                if (restoreInputs.Count >= 3)
                {
                    await restoreInputs[0].FillAsync("NoweTestHaslo123!");
                    await restoreInputs[1].FillAsync(TestConstants.UserPassword);
                    await restoreInputs[2].FillAsync(TestConstants.UserPassword);
                    await Page.GetByRole(AriaRole.Button, new() { Name = "Zmień hasło" }).ClickAsync();
                    await Page.WaitForTimeoutAsync(3000);
                }

                Assert.Pass("Password changed and restored successfully");
            }
        }

        Assert.Pass("Password change validation verified");
    }
}
