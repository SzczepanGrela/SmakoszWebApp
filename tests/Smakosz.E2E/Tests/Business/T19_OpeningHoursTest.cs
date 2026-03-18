using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T19_OpeningHoursTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanViewAndSaveOpeningHours()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/hours");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Godziny otwarcia") || pageContent.Contains("godziny"),
            Is.True, "Opening hours page should load");

        var timeInputs = Page.Locator("input[type='time']");
        var timeCount = await timeInputs.CountAsync();
        Assert.That(timeCount, Is.GreaterThan(0),
            "Opening hours form should contain time inputs");

        var saveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Zapisz godziny" });
        await Expect(saveButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await saveButton.ClickAsync();

        await AssertToastAsync("Godziny otwarcia zostały zaktualizowane");
    }
}
