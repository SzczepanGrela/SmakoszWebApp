using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T123_AdminUserDetailInlineEmailEditTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_InlineEditsEmail_SuccessAndConflict()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/users?search=anna-nowak");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var annaRow = Page.Locator("tr", new() { HasText = "anna-nowak" }).First;
        await Expect(annaRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        var detailLink = annaRow.Locator("a[href*='/admin/users/']").First;
        await detailLink.ClickAsync();

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(3000);

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("anna-nowak", new LocatorAssertionsToContainTextOptions { Timeout = 10_000 });

        var emailPencil = Page.Locator("button[title='Zmień email']").First;
        await Expect(emailPencil).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await emailPencil.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var emailInput = Page.Locator("input[type='email']").First;
        await Expect(emailInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        var freshEmail = $"anna-changed-{Guid.NewGuid():N}@smakosz.test";
        await emailInput.FillAsync(freshEmail);

        var saveButton = Page.Locator("button.btn-warning", new() { HasText = "Zapisz" }).First;
        await saveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2500);
        await WaitForBlazorLoadedAsync();

        await AssertToastAsync("Email zmieniony");

        await Page.WaitForTimeoutAsync(1500);
        emailPencil = Page.Locator("button[title='Zmień email']").First;
        await Expect(emailPencil).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await emailPencil.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        emailInput = Page.Locator("input[type='email']").First;
        await Expect(emailInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await emailInput.FillAsync(TestConstants.UserEmail);

        saveButton = Page.Locator("button.btn-warning", new() { HasText = "Zapisz" }).First;
        await saveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2500);
        await WaitForBlazorLoadedAsync();

        var errorToast = Page.Locator(".toast").First;
        await Expect(errorToast).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        var inputAfterConflict = Page.Locator("input[type='email']").First;
        await Expect(inputAfterConflict).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
    }
}
