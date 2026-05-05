using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T03_BusinessDishManagementTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessOwner_CanCreateEditAndDeleteDish()
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

        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dashboard");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Dashboard") || pageContent.Contains("dashboard") || pageContent.Contains("Panel"),
            Is.True, $"Dashboard page should load for restaurant owner.\nAPI calls:\n{string.Join("\n", apiCalls)}");

        await NavigateAndWaitAsync("/restaurant/dishes/add");
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("label:has-text('Nazwa dania') + input, label:has-text('Nazwa dania') ~ input").First;
        if (!await nameInput.IsVisibleAsync())
            nameInput = Page.Locator("input[type='text'].form-control").First;
        await nameInput.FillAsync("Spaghetti Carbonara");

        await Page.Locator("textarea.form-control").FillAsync("Klasyczne wloskie spaghetti z sosem carbonara.");

        var priceInput = Page.Locator("input[type='number'][step='0.01']").First;
        await priceInput.FillAsync("32.50");

        var caloriesInput = Page.Locator("input[type='number']:not([step])").First;
        if (!await caloriesInput.IsVisibleAsync())
            caloriesInput = Page.Locator("input[type='number']").Nth(1);
        await caloriesInput.FillAsync("720");

        await Page.Locator("#dishCategory").SelectOptionAsync(new SelectOptionValue { Index = 1 });

        apiCalls.Clear();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj danie" }).ClickAsync();

        var dishRedirectTask = Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/add"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var dishErrorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(dishRedirectTask, dishErrorTask);

        if (Page.Url.Contains("/add"))
        {
            var errorVisible = await Page.Locator(".alert-danger").First.IsVisibleAsync();
            var errorText = errorVisible
                ? await Page.Locator(".alert-danger").First.TextContentAsync()
                : "Unknown error (no redirect, no error alert)";
            Assert.Fail($"Dish creation failed: {errorText}\nAPI calls:\n{string.Join("\n", apiCalls)}");
        }

        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Spaghetti Carbonara");

        var dishRow = Page.Locator("tr", new() { HasText = "Spaghetti Carbonara" });
        var editButton = dishRow.Locator("a.btn-outline-primary").First;
        await editButton.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/restaurant/dishes/edit/"),
            new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        var editPriceInput = Page.Locator("input[type='number'][step='0.01']").First;
        await editPriceInput.ClearAsync();
        await editPriceInput.FillAsync("34.90");

        var editDescription = Page.Locator("textarea.form-control").First;
        await editDescription.ClearAsync();
        await editDescription.FillAsync("Spaghetti carbonara z pancetta i parmezanem.");

        apiCalls.Clear();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Zapisz zmiany" }).ClickAsync();

        var editRedirectTask = Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/edit"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var editErrorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(editRedirectTask, editErrorTask);

        if (Page.Url.Contains("/edit"))
        {
            var errorVisible = await Page.Locator(".alert-danger").First.IsVisibleAsync();
            var errorText = errorVisible
                ? await Page.Locator(".alert-danger").First.TextContentAsync()
                : "Unknown error (no redirect from edit page)";
            Assert.Fail($"Dish edit save failed: {errorText}\nURL: {Page.Url}\nAPI calls:\n{string.Join("\n", apiCalls)}");
        }

        await WaitForBlazorLoadedAsync();

        var dishRowAfterEdit = Page.Locator("tr", new() { HasText = "Spaghetti Carbonara" });
        var deleteButton = dishRowAfterEdit.Locator("button.btn-outline-danger").First;
        await deleteButton.ClickAsync();

        await AssertPageContainsTextAsync("Czy na pewno chcesz usunąć danie");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź" }).ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await AssertToastAsync("Danie zostało usunięte.");

        var deletedDish = Page.Locator("tr", new() { HasText = "Spaghetti Carbonara" });
        await Expect(deletedDish).ToHaveCountAsync(0);
    }
}
