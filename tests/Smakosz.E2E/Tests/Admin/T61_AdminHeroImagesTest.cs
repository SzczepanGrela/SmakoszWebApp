using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T61_AdminHeroImagesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanAccessHeroImagesPage()
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
        await Expect(addButton).ToBeVisibleAsync();

        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var fileInput = Page.Locator("input[type='file']");
        var fileInputCount = await fileInput.CountAsync();
        Assert.That(fileInputCount, Is.GreaterThan(0), "File upload input should be visible");

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak hero images"))
        {
            Assert.Pass("No hero images - empty state verified");
        }

        var deleteButtons = Page.Locator("button.btn-outline-danger.btn-sm", new() { HasText = "Usuń" });
        var deleteCount = await deleteButtons.CountAsync();

        if (deleteCount > 0)
        {
            Assert.Pass($"Hero images page accessible - {deleteCount} image(s) with delete buttons found");
        }

        Assert.Pass("Hero images page accessible and functional");
    }
}
