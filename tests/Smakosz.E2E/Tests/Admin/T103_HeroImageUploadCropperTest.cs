using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T103_HeroImageUploadCropperTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_HeroImagesPage_OpensCropperWithCreditField()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/hero-images");
        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/hero-images");
        }
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Hero images");

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj nowe" });
        await Expect(addButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(800);

        await AssertPageContainsTextAsync("Przytnij hero image (21:9)");

        var fileInput = Page.Locator("input[type='file']").First;
        await Expect(fileInput).ToBeAttachedAsync();

        await AssertPageContainsTextAsync("Credit text (opcjonalnie)");

        var cancelButton = Page.GetByRole(AriaRole.Button, new() { Name = "Anuluj" }).First;
        await Expect(cancelButton).ToBeVisibleAsync();
    }
}
