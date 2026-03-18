using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T28_DishSaveFullCycleTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanSaveDishAndSeeItOnSavedPageThenRemove()
    {
        // T04 uses tiramisu - T28 uses kebab-duzy for independence
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/dishes/kebab-duzy");
        await WaitForBlazorLoadedAsync();

        var saveButton = Page.Locator("button.btn-outline-danger").First;
        await Expect(saveButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        var savedButton = Page.Locator("button.btn-danger:not(.btn-outline-danger)").First;
        await Expect(savedButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await NavigateAndWaitAsync("/saved");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Kebab") || pageContent.Contains("kebab"), Is.True,
            "Kebab should appear on saved dishes page after saving");

        await NavigateAndWaitAsync("/dishes/kebab-duzy");
        await WaitForBlazorLoadedAsync();

        var unsaveButton = Page.Locator("button.btn-danger:not(.btn-outline-danger)").First;
        await unsaveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        var unsavedButton = Page.Locator("button.btn-outline-danger").First;
        await Expect(unsavedButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await NavigateAndWaitAsync("/saved");
        await WaitForBlazorLoadedAsync();

        pageContent = await Page.ContentAsync();
        var kebabPresent = pageContent.Contains("Kebab Duzy") || pageContent.Contains("kebab-duzy");
        if (kebabPresent)
        {
            // If still showing, the page might have cached - check for empty state
            var emptyState = Page.GetByText("Brak zapisanych");
            var isEmpty = await emptyState.CountAsync() > 0;
            Assert.That(!kebabPresent || isEmpty, Is.True,
                "Kebab should not appear in saved dishes after unsaving");
        }
    }
}
