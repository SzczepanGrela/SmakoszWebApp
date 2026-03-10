using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T70_OwnFollowersPageTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewOwnFollowingAndFollowersPages()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/following");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/following");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Obserwowani");

        var backButton = Page.GetByText("Powrót do profilu").First;
        var backCount = await backButton.CountAsync();
        Assert.That(backCount, Is.GreaterThan(0), "Should show 'Powrót do profilu' button");

        var pageContent = await Page.ContentAsync();
        var hasContent = pageContent.Contains("Nie obserwujesz nikogo") ||
                         await Page.Locator(".card, .list-group-item").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Should show user cards or empty state");

        await NavigateAndWaitAsync("/followers");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Obserwujący");

        backButton = Page.GetByText("Powrót do profilu").First;
        backCount = await backButton.CountAsync();
        Assert.That(backCount, Is.GreaterThan(0), "Should show 'Powrót do profilu' button on followers page");

        pageContent = await Page.ContentAsync();
        var hasFollowersContent = pageContent.Contains("Nie masz jeszcze obserwujących") ||
                                   await Page.Locator(".card, .list-group-item").CountAsync() > 0;
        Assert.That(hasFollowersContent, Is.True, "Should show user cards or empty state");
    }
}
