using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T69_PublicFollowerFollowingPagesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Anonymous_CanViewPublicFollowingAndFollowersPages()
    {
        await NavigateAndWaitAsync($"/users/{TestConstants.UserUsername}/following");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains(TestConstants.UserUsername), Is.True,
            "Following page should contain the username");

        var hasFollowingHeading = pageContent.Contains("Obserwowani");
        Assert.That(hasFollowingHeading, Is.True, "Should show 'Obserwowani' heading");

        var hasFollowingContent = pageContent.Contains("Nie obserwuje nikogo") ||
                                  await Page.Locator(".card, .list-group-item").CountAsync() > 0;
        Assert.That(hasFollowingContent, Is.True,
            "Should show user cards or empty state");

        await NavigateAndWaitAsync($"/users/{TestConstants.UserUsername}/followers");
        await WaitForBlazorLoadedAsync();

        pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains(TestConstants.UserUsername), Is.True,
            "Followers page should contain the username");

        var hasFollowersHeading = pageContent.Contains("Obserwujący");
        Assert.That(hasFollowersHeading, Is.True, "Should show 'Obserwujący' heading");

        var hasFollowersContent = pageContent.Contains("Brak obserwujących") ||
                                   await Page.Locator(".card, .list-group-item").CountAsync() > 0;
        Assert.That(hasFollowersContent, Is.True,
            "Should show user cards or empty state");
    }
}
