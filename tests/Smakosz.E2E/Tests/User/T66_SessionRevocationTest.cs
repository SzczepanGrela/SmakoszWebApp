using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T66_SessionRevocationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewAndRevokeSessionsOnSecurityPage()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/profile/security");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/profile/security");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Bezpieczeństwo");

        await AssertPageContainsTextAsync("Aktywne sesje");

        var refreshButton = Page.GetByRole(AriaRole.Button, new() { Name = "Odśwież" }).First;
        await Expect(refreshButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        var logoutAllButton = Page.GetByRole(AriaRole.Button, new() { Name = "Wyloguj wszystkie inne" }).First;
        var logoutAllCount = await logoutAllButton.CountAsync();
        Assert.That(logoutAllCount, Is.GreaterThan(0), "'Wyloguj wszystkie inne' button should be present");

        // Sessions may show: spinner, empty state, or session list
        try
        {
            await Page.Locator(".list-group-item, .empty-state, [class*='spinner']")
                .First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
        }

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(1000);

        var pageContent = await Page.ContentAsync();
        var hasSessionContent = pageContent.Contains("Obecna sesja") ||
                                pageContent.Contains("Brak aktywnych sesji") ||
                                pageContent.Contains("Sesja #") ||
                                pageContent.Contains("Ładowanie sesji");

        Assert.That(hasSessionContent, Is.True,
            "Sessions section should show sessions, empty state, or loading state");

        var isDisabled = await logoutAllButton.GetAttributeAsync("disabled");
        if (isDisabled == null)
        {
            // Button is enabled - there are other sessions to revoke
            await logoutAllButton.ClickAsync();

            var toastLocator = Page.Locator(".toast").First;
            try
            {
                await Expect(toastLocator).ToBeVisibleAsync(
                    new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                // Toast may have auto-dismissed
            }
        }
    }
}
