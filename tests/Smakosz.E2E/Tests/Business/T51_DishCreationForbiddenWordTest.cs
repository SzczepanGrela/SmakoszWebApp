using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T51_DishCreationForbiddenWordTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanCreateDish_WithCleanText()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);
        await NavigateAndWaitAsync("/restaurant/dishes/add");
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("label:has-text('Nazwa dania') + input, label:has-text('Nazwa dania') ~ input").First;
        if (!await nameInput.IsVisibleAsync())
            nameInput = Page.Locator("input[type='text'].form-control").First;
        await nameInput.FillAsync("Pizza Quattro Formaggi");

        await Page.Locator("textarea.form-control").FillAsync("Pizza z czterema serami.");

        var priceInput = Page.Locator("input[type='number'][step='0.01']").First;
        await priceInput.FillAsync("35.00");

        await Page.Locator("#dishCategory").SelectOptionAsync(new SelectOptionValue { Index = 1 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj danie" }).ClickAsync();

        var redirectTask = Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/add"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var errorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(redirectTask, errorTask);

        if (!Page.Url.Contains("/add"))
        {
            await WaitForBlazorLoadedAsync();
            await AssertPageContainsTextAsync("Pizza Quattro Formaggi");
        }
        else
        {
            Assert.Fail("Dish creation with clean text should succeed");
        }
    }

    [Test]
    public async Task BusinessOwner_CannotCreateDish_WithForbiddenWord()
    {
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);
        await NavigateAndWaitAsync("/restaurant/dishes/add");
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("label:has-text('Nazwa dania') + input, label:has-text('Nazwa dania') ~ input").First;
        if (!await nameInput.IsVisibleAsync())
            nameInput = Page.Locator("input[type='text'].form-control").First;
        await nameInput.FillAsync("Pizza kurwa dobra");

        var priceInput = Page.Locator("input[type='number'][step='0.01']").First;
        await priceInput.FillAsync("25.00");

        await Page.Locator("#dishCategory").SelectOptionAsync(new SelectOptionValue { Index = 1 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj danie" }).ClickAsync();

        await Page.WaitForTimeoutAsync(3000);

        var pageContent = await Page.ContentAsync();
        Assert.That(
            pageContent.Contains("niedozwolone") || pageContent.Contains("FORBIDDEN") ||
            Page.Url.Contains("/add"),
            Is.True, "Dish creation with forbidden word should fail");
    }
}
