using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T55_NotificationsAndSettingsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewNotificationsAndSaveSettings()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/notifications");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Powiadomienia");

        var emptyState = Page.GetByText("Brak powiadomień");
        var settingsLink = Page.GetByRole(AriaRole.Button, new() { Name = "Ustawienia" })
            .Or(Page.Locator("a[href='/profile/notifications']").Filter(new() { HasText = "Ustawienia" }));

        var isEmpty = await emptyState.CountAsync() > 0;
        if (isEmpty)
        {
            await Expect(emptyState.First).ToBeVisibleAsync();
        }

        await Expect(settingsLink.First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await NavigateAndWaitAsync("/profile/notifications");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Ustawienia powiadomień");

        // use list-group selector to avoid matching hidden navbar items
        var sidebar = Page.Locator(".list-group");
        await Expect(sidebar.GetByText("Profil").First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await Expect(sidebar.GetByText("Bezpieczeństwo").First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await AssertPageContainsTextAsync("Polubienia");
        await AssertPageContainsTextAsync("Nowi obserwujący");
        await AssertPageContainsTextAsync("Systemowe");

        var toggles = Page.Locator(".form-check-input[type='checkbox'][role='switch']");
        var toggleCount = await toggles.CountAsync();
        Assert.That(toggleCount, Is.GreaterThanOrEqualTo(3),
            $"Expected at least 3 toggle switches, found {toggleCount}");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zapisz ustawienia" }).ClickAsync();

        await Page.WaitForTimeoutAsync(2000);

        var pageContent = await Page.ContentAsync();
        var hasSuccess = pageContent.Contains("Ustawienia zostały zapisane")
                         || pageContent.Contains("Ustawienia powiadomień zostały zapisane");
        Assert.That(hasSuccess, Is.True,
            "Expected success message after saving notification settings");
    }
}
