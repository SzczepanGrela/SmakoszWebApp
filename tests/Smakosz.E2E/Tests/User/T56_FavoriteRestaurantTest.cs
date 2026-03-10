using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T56_FavoriteRestaurantTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanFavoriteAndUnfavoriteRestaurant()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        var addFavButton = Page.Locator("button.btn-sm.btn-outline-danger").Filter(
            new() { HasText = "Dodaj do ulubionych" });
        var isFavButton = Page.Locator("button.btn-sm.btn-danger:not(.btn-outline-danger)").Filter(
            new() { HasText = "Ulubiona" });

        // Determine initial state
        var isAlreadyFavorite = await isFavButton.CountAsync() > 0;

        if (isAlreadyFavorite)
        {
            // Unfavorite first so we start from a clean state
            await isFavButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
            await Expect(addFavButton).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        }

        await Expect(addFavButton).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addFavButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert button changed to "Ulubiona" (solid btn-danger)
        await Expect(isFavButton).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await WaitForBlazorLoadedAsync();

        var isFavAfterReload = Page.Locator("button.btn-sm.btn-danger:not(.btn-outline-danger)").Filter(
            new() { HasText = "Ulubiona" });
        await Expect(isFavAfterReload).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await isFavAfterReload.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert button reverted to "Dodaj do ulubionych"
        var addFavAfterUnfav = Page.Locator("button.btn-sm.btn-outline-danger").Filter(
            new() { HasText = "Dodaj do ulubionych" });
        await Expect(addFavAfterUnfav).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
