using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T18_DishDeleteProtectionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanDeleteDish_WithConfirmation()
    {
        // First create a dish via UI so we don't affect other tests' seed data.

        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dishes/add");
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("input[type='text'].form-control").First;
        await nameInput.FillAsync("Danie Do Usuniecia");
        await Page.Locator("textarea.form-control").FillAsync("Opis dania tymczasowego.");
        await Page.Locator("input[type='number'][step='0.01']").First.FillAsync("15.00");
        var caloriesInput = Page.Locator("input[type='number']:not([step])").First;
        if (!await caloriesInput.IsVisibleAsync())
            caloriesInput = Page.Locator("input[type='number']").Nth(1);
        await caloriesInput.FillAsync("300");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj danie" }).ClickAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/add"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        await WaitForBlazorLoadedAsync();

        // Assert dish was created
        await AssertPageContainsTextAsync("Danie Do Usuniecia");

        var dishRow = Page.Locator("tr", new() { HasText = "Danie Do Usuniecia" });
        var deleteButton = dishRow.Locator("button.btn-outline-danger").First;
        await deleteButton.ClickAsync();

        await AssertPageContainsTextAsync("Czy na pewno chcesz usunac danie");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Potwierdz" }).ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await AssertToastAsync("Danie zostalo usuniete.");

        var deletedDish = Page.Locator("tr", new() { HasText = "Danie Do Usuniecia" });
        await Expect(deletedDish).ToHaveCountAsync(0);
    }
}
