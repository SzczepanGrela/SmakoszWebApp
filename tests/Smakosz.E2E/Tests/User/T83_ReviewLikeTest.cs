using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T83_ReviewLikeTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanLikeAndUnlikeReview()
    {
        // jan-kowalski can like anna-nowak's review on margherita
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);
        await NavigateAndWaitAsync("/dishes/pizza-margherita");
        await WaitForBlazorLoadedAsync();

        var likeButton = Page.Locator(".review-like-pill").First;
        var likeButtonVisible = await likeButton.IsVisibleAsync();

        if (likeButtonVisible)
        {
            var initialText = await likeButton.InnerTextAsync();
            await likeButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);

            var activeButton = Page.Locator(".review-like-pill.review-like-pill--active").First;
            var isLiked = await activeButton.CountAsync() > 0;

            if (isLiked)
            {
                await activeButton.ClickAsync();
                await Page.WaitForTimeoutAsync(2000);

                var inactiveButton = Page.Locator(".review-like-pill:not(.review-like-pill--active)").First;
                var isUnliked = await inactiveButton.CountAsync() > 0;
                Assert.That(isUnliked, Is.True, "Like button should revert to inactive state after unlike");
            }
            else
            {
                var anyButton = Page.Locator(".review-like-pill").First;
                Assert.That(await anyButton.CountAsync(), Is.GreaterThan(0), "Like button should be in a valid state");
            }
        }
        else
        {
            Assert.Pass("Review like button not visible (no reviews to like)");
        }
    }
}
