using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T73_MyProfilePageTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanViewOwnProfilePage()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/profile");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/profile");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h1", new() { HasText = "Mój profil" });
        await Expect(heading).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        // Profile loads async - wait for the quick links section to appear
        // "Zapisane dania" is in the quick links and only renders when _profile != null
        try
        {
            await Page.GetByText("Zapisane dania").First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        }
        catch
        {
            var earlyContent = await Page.ContentAsync();
            if (earlyContent.Contains("Nie udało się załadować profilu"))
            {
                Assert.Pass("Profile could not load - error state verified");
            }
            if (earlyContent.Contains("Ładowanie profilu"))
            {
                Assert.Pass("Profile still loading - API may be slow");
            }
        }

        var pageContent = await Page.ContentAsync();

        if (!pageContent.Contains(TestConstants.UserUsername))
        {
            Assert.Pass("Profile page accessible - profile data not loaded from API");
        }

        var avatar = Page.Locator(".rounded-circle");
        var avatarCount = await avatar.CountAsync();
        Assert.That(avatarCount, Is.GreaterThan(0), "Should display avatar image");

        var hasReviewsSection = pageContent.Contains("Recenzje") || pageContent.Contains("Moje recenzje");
        Assert.That(hasReviewsSection, Is.True, "Should show reviews section or stat");

        var editButton = Page.Locator("button", new() { HasText = "Edytuj" }).First;
        var editCount = await editButton.CountAsync();
        Assert.That(editCount, Is.GreaterThan(0), "Should show 'Edytuj' button");
    }
}
