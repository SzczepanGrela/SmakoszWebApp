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

        var isAlreadyFavorite = await isFavButton.CountAsync() > 0;

        if (isAlreadyFavorite)
        {
            await isFavButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
            await Expect(addFavButton).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        }

        await Expect(addFavButton).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addFavButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

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

        var addFavAfterUnfav = Page.Locator("button.btn-sm.btn-outline-danger").Filter(
            new() { HasText = "Dodaj do ulubionych" });
        await Expect(addFavAfterUnfav).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
