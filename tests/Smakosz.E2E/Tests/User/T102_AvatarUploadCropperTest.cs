using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T102_AvatarUploadCropperTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_AvatarPicker_OpensCropperModal()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/settings");
        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/settings");
        }
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Zdjęcie profilowe");

        var openButton = Page.GetByRole(AriaRole.Button,
            new() { NameRegex = new System.Text.RegularExpressions.Regex("(Dodaj avatar|Zmień avatar)") }).First;
        await Expect(openButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await openButton.ClickAsync();
        await Page.WaitForTimeoutAsync(800);

        await AssertPageContainsTextAsync("Przytnij avatar");

        var fileInput = Page.Locator("input[type='file']").First;
        await Expect(fileInput).ToBeAttachedAsync();

        var cancelButton = Page.GetByRole(AriaRole.Button, new() { Name = "Anuluj" }).First;
        await Expect(cancelButton).ToBeVisibleAsync();
    }
}
