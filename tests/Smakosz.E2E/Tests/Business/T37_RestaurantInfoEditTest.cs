using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T37_RestaurantInfoEditTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanEditRestaurantInfo()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/info");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/restaurant/info");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Informacje o restauracji");

        // Assert form loaded with restaurant name
        var nameInput = Page.Locator("input.form-control[type='text']").First;
        await Expect(nameInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        var nameValue = await nameInput.InputValueAsync();
        Assert.That(nameValue, Does.Contain("Pizzeria Roma"),
            $"Restaurant name input should contain 'Pizzeria Roma', got: {nameValue}");

        // Change phone number
        var phoneInput = Page.Locator("input[type='tel'].form-control").First;
        await phoneInput.ClearAsync();
        await phoneInput.FillAsync("+48 123 456 789");

        var saveButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Zapisz zmiany") }).First;
        await saveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);

        // Assert success
        await AssertToastAsync("Informacje zostały zaktualizowane.");
    }
}
