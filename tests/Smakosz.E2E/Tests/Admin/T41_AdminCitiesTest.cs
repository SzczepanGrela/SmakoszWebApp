using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T41_AdminCitiesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanCrudCities()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/cities");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/cities");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading and existing cities
        await AssertPageContainsTextAsync("Miasta");
        await Page.WaitForTimeoutAsync(2000);

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Warszawa"), Is.True, "Should show Warszawa from seed");
        Assert.That(pageContent.Contains("Krakow"), Is.True, "Should show Krakow from seed");

        using var http = new HttpClient();
        var adminToken = E2EAuthHelper.GenerateAdminToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createPayload = JsonSerializer.Serialize(new { Name = "Gdansk", Region = "Pomorskie" });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/cities",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)createResponse.StatusCode, Is.LessThan(300),
            $"Create city should succeed: {createResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/cities");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Gdansk");

        var citiesResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/admin/cities");
        var citiesJson = await citiesResponse.Content.ReadAsStringAsync();
        var gdanskId = 0;

        using var doc = JsonDocument.Parse(citiesJson);
        // ApiResponse<PagedResult<T>>: root.data = PagedResult, root.data.data = T[]
        foreach (var city in doc.RootElement.GetProperty("data").GetProperty("data").EnumerateArray())
        {
            if (city.GetProperty("cityName").GetString() == "Gdansk")
            {
                gdanskId = city.GetProperty("id").GetInt32();
                break;
            }
        }

        Assert.That(gdanskId, Is.GreaterThan(0), "Should find Gdansk city ID");

        var updatePayload = JsonSerializer.Serialize(new { Region = "Pomorze" });
        var updateResponse = await http.PutAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/cities/{gdanskId}",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));
        Assert.That((int)updateResponse.StatusCode, Is.LessThan(300),
            $"Update city should succeed: {updateResponse.StatusCode}");

        var deleteResponse = await http.DeleteAsync($"{TestConstants.ApiBaseUrl}/api/admin/cities/{gdanskId}");
        Assert.That((int)deleteResponse.StatusCode, Is.LessThan(300),
            $"Delete city should succeed: {deleteResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/cities");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var finalContent = await Page.ContentAsync();
        Assert.That(finalContent.Contains("Gdansk"), Is.False, "Gdansk should be deleted");

        // Assert Warszawa delete button is disabled (has restaurants)
        var warszawaRow = Page.Locator("tr", new() { HasText = "Warszawa" });
        var warszawaDeleteBtn = warszawaRow.Locator("button.btn-outline-danger").First;
        var warszawaDisabled = await warszawaDeleteBtn.GetAttributeAsync("disabled");
        Assert.That(warszawaDisabled, Is.Not.Null,
            "Delete button for Warszawa (has restaurants) should be disabled");
    }
}
