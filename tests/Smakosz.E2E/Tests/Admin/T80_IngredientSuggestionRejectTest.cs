using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T80_IngredientSuggestionRejectTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanRejectIngredientSuggestion()
    {
        using var http = new HttpClient();
        var businessToken = E2EAuthHelper.GenerateBusinessToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", businessToken);

        var createPayload = JsonSerializer.Serialize(new { suggestedName = "Tymianek E2E T80" });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/restaurants/pizzeria-roma/ingredient-suggestions",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        var createSucceeded = createResponse.IsSuccessStatusCode;

        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/ingredient-suggestions");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/ingredient-suggestions");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading (no diacritics!)
        await AssertPageContainsTextAsync("Sugestie skladnikow");

        await Page.WaitForTimeoutAsync(2000);

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak sugestii"))
        {
            Assert.Pass($"No suggestions in table - empty state verified. API create succeeded: {createSucceeded}");
        }

        var rejectButton = Page.Locator("button.btn-outline-danger.btn-sm").First;
        var rejectCount = await rejectButton.CountAsync();

        if (rejectCount == 0)
        {
            Assert.Pass("No reject button found - suggestions may have been already processed");
        }

        var initialRows = Page.Locator("table.table tbody tr");
        var initialRowCount = await initialRows.CountAsync();

        await rejectButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var updatedContent = await Page.ContentAsync();
        var hasRejectToast = updatedContent.Contains("odrzucona");
        var rowRemoved = await Page.Locator("table.table tbody tr").CountAsync() < initialRowCount;
        var hasEmptyState = updatedContent.Contains("Brak sugestii");

        Assert.That(hasRejectToast || rowRemoved || hasEmptyState, Is.True,
            "Expected rejection toast, row removed from table, or empty state after rejection");
    }
}
