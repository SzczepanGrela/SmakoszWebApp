using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T17_DishEditTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanEditExistingDish()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dishes");
        await WaitForBlazorLoadedAsync();

        var table = Page.Locator("table").First;
        await Expect(table).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await Page.WaitForTimeoutAsync(2000);
        await AssertPageContainsTextAsync("Pizza Margherita");

        var dishRow = Page.Locator("tr", new() { HasText = "Margherita" });
        await Expect(dishRow.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var editButton = dishRow.First.Locator("a.btn-outline-primary").First;
        await editButton.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/restaurant/dishes/edit/"),
            new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("input[type='text'].form-control").First;
        await Page.WaitForTimeoutAsync(1000); // Wait for form data to load
        var nameValue = await nameInput.InputValueAsync();
        Assert.That(nameValue, Does.Contain("Margherita"),
            $"Name field should be pre-filled. Got: {nameValue}");

        var priceInput = Page.Locator("input[type='number'][step='0.01']").First;
        await priceInput.ClearAsync();
        await priceInput.FillAsync("26.90");

        var descriptionInput = Page.Locator("textarea.form-control").First;
        await descriptionInput.ClearAsync();
        await descriptionInput.FillAsync("Zaktualizowany opis z testu E2E - pizza margherita premium.");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zapisz zmiany" }).ClickAsync();

        var redirectTask = Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/edit"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var errorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(redirectTask, errorTask);

        if (Page.Url.Contains("/edit"))
        {
            var errorVisible = await Page.Locator(".alert-danger").First.IsVisibleAsync();
            if (errorVisible)
            {
                var errorText = await Page.Locator(".alert-danger").First.TextContentAsync();
                Assert.Fail($"Dish edit failed: {errorText}");
            }
        }

        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("26,90") || pageContent.Contains("26.90"),
            Is.True, "Updated price should be visible in dish list");
    }
}
