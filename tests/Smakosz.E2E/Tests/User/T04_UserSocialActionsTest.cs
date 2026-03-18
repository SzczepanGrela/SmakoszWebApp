using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T04_UserSocialActionsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task LoggedInUser_CanSaveDish_FollowUser_AndUnsave()
    {
        var apiCalls = new List<string>();
        Page.Response += async (_, response) =>
        {
            if (response.Url.Contains("/api/"))
            {
                try
                {
                    var body = await response.TextAsync();
                    apiCalls.Add($"[{response.Status}] {response.Request.Method} {response.Url} -> {body[..Math.Min(500, body.Length)]}");
                }
                catch
                {
                    apiCalls.Add($"[{response.Status}] {response.Request.Method} {response.Url} -> (body unreadable)");
                }
            }
        };

        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/dishes/tiramisu");
        await WaitForBlazorLoadedAsync();

        var saveButton = Page.Locator("button.btn-outline-danger").First;
        await Expect(saveButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        apiCalls.Clear();
        await saveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);

        var savedButton = Page.Locator("button.btn-danger:not(.btn-outline-danger)").First;
        var isSaved = await savedButton.IsVisibleAsync();

        if (!isSaved)
        {
            var bodyText = await Page.Locator("body").InnerTextAsync();
            Assert.Fail(
                $"SaveDishButton did not change to saved state after clicking.\n" +
                $"API calls after click:\n{string.Join("\n", apiCalls)}\n" +
                $"Page body (first 500): {bodyText[..Math.Min(500, bodyText.Length)]}");
        }

        await NavigateAndWaitAsync("/saved");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Tiramisu");

        await NavigateAndWaitAsync("/users/anna-nowak");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("anna-nowak");

        var followButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Obserwuj$") }).First;
        var isFollowVisible = await followButton.IsVisibleAsync();

        if (isFollowVisible)
        {
            apiCalls.Clear();
            await followButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1500);

            await NavigateAndWaitAsync("/following");
            await WaitForBlazorLoadedAsync();

            await AssertPageContainsTextAsync("anna-nowak");
        }

        await NavigateAndWaitAsync("/dishes/tiramisu");
        await WaitForBlazorLoadedAsync();

        var unsaveButton = Page.Locator("button.btn-danger:not(.btn-outline-danger)").First;
        await unsaveButton.ClickAsync();

        var unsavedButton = Page.Locator("button.btn-outline-danger").First;
        await Expect(unsavedButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await NavigateAndWaitAsync("/saved");
        await WaitForBlazorLoadedAsync();

        var tiramisuOnPage = Page.GetByText("Tiramisu");
        var count = await tiramisuOnPage.CountAsync();
        Assert.That(count, Is.EqualTo(0), "Tiramisu should not appear in saved dishes after unsaving");
    }
}
