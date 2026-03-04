using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T16_FollowUnfollowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanFollowAndUnfollow_OnSameProfilePage()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/users/anna-nowak");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("anna-nowak");

        var followBtn = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Obserwuj") }).First;
        await Expect(followBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var initialText = await followBtn.TextContentAsync();
        if (initialText!.Contains("Obserwujesz"))
        {
            // Already following - unfollow first to reset state
            await followBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(1500);
        }

        var obserwujBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Obserwuj" }).First;
        await Expect(obserwujBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await obserwujBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var obserwujeszBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Obserwujesz" }).First;
        await Expect(obserwujeszBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await obserwujeszBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var unfollowedBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Obserwuj" }).First;
        await Expect(unfollowedBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
