using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T42_AdminIngredientsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanCrudIngredients()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/ingredients");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/ingredients");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Skladniki");
        await Page.WaitForTimeoutAsync(2000);

        using var http = new HttpClient();
        var adminToken = E2EAuthHelper.GenerateAdminToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createPayload = JsonSerializer.Serialize(new
        {
            Name = "Bazyli testowy",
            IsAllergen = false,
            IsVegetarian = true,
            IsVegan = true,
            IsGlutenFree = true,
            IsLactoseFree = true
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/ingredients",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)createResponse.StatusCode, Is.LessThan(300),
            $"Create ingredient should succeed: {createResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/ingredients");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var bazyliRow = Page.Locator("tr", new() { HasText = "Bazyli testowy" });
        await Expect(bazyliRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var rowText = await bazyliRow.InnerTextAsync();
        Assert.That(rowText, Does.Contain("Tak"),
            "Bazyli testowy should show 'Tak' for vegetarian");

        var ingredientsResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/admin/ingredients");
        var ingredientsJson = await ingredientsResponse.Content.ReadAsStringAsync();
        var bazyliId = 0;

        using var doc = JsonDocument.Parse(ingredientsJson);
        // ApiResponse<PagedResult<T>>: root.data = PagedResult, root.data.data = T[]
        foreach (var ingredient in doc.RootElement.GetProperty("data").GetProperty("data").EnumerateArray())
        {
            if (ingredient.GetProperty("ingredientName").GetString() == "Bazyli testowy")
            {
                bazyliId = ingredient.GetProperty("ingredientId").GetInt32();
                break;
            }
        }

        Assert.That(bazyliId, Is.GreaterThan(0), "Should find Bazyli testowy ingredient ID");

        var deleteResponse = await http.DeleteAsync($"{TestConstants.ApiBaseUrl}/api/admin/ingredients/{bazyliId}");
        Assert.That((int)deleteResponse.StatusCode, Is.LessThan(300),
            $"Delete ingredient should succeed: {deleteResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/ingredients");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var bazyliAfterDelete = Page.Locator("tr", new() { HasText = "Bazyli testowy" });
        await Expect(bazyliAfterDelete).ToHaveCountAsync(0);
    }
}
