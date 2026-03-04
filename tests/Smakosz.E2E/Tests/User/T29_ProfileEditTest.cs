using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T29_ProfileEditTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewAndSaveProfileSettings()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/profile/settings");
        await WaitForBlazorLoadedAsync();

        // Assert heading
        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Ustawienia") || pageContent.Contains("Profil"), Is.True,
            "Profile settings page should show heading");

        // Assert form is visible with personal info section
        Assert.That(
            pageContent.Contains("Informacje osobiste") || pageContent.Contains("osobiste") || pageContent.Contains("Imie"),
            Is.True,
            "Profile settings should show personal information section");

        // Assert submit button visible
        var submitButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Zapisz") }).First;
        await Expect(submitButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await submitButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert success message
        pageContent = await Page.ContentAsync();
        var hasSuccess = pageContent.Contains("zapisane") || pageContent.Contains("Zapisane") ||
                        pageContent.Contains("zaktualizowane") || pageContent.Contains("success");
        Assert.That(hasSuccess, Is.True,
            "Saving profile should show success feedback");
    }
}
