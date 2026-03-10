using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T34_DishAvailabilityTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanToggleDishAvailability()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dishes");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/restaurant/dishes");
        }

        await WaitForBlazorLoadedAsync();

        await Page.Locator("table.table tbody tr").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        var margheritaRow = Page.Locator("tr", new() { HasText = "Margherita" });
        await Expect(margheritaRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        // Locate the toggle switch
        var toggleCheckbox = margheritaRow.Locator(".form-check-input[type='checkbox']").First;
        await Expect(toggleCheckbox).ToBeVisibleAsync();

        // Assert checkbox is checked (dish is available)
        var isChecked = await toggleCheckbox.IsCheckedAsync();
        Assert.That(isChecked, Is.True, "Pizza Margherita should be available initially");

        await toggleCheckbox.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert toast "Danie zostało ukryte."
        await AssertToastAsync("Danie zostało ukryte.");

        await toggleCheckbox.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert toast "Danie jest teraz dostępne."
        await AssertToastAsync("Danie jest teraz dostępne.");
    }
}
