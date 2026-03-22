using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T95_AdminTagsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanCrudTags()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/tags");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/tags");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Tagi");
        await Page.WaitForTimeoutAsync(2000);

        using var http = new HttpClient();
        var adminToken = E2EAuthHelper.GenerateAdminToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createPayload = JsonSerializer.Serialize(new { Name = "E2ETag", Category = "Test", TargetEntity = "Both" });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/tags",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)createResponse.StatusCode, Is.LessThan(300),
            $"Create tag should succeed: {createResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/tags");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("E2ETag");

        var tagsResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/admin/tags");
        var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
        var tagId = 0;

        using var doc = JsonDocument.Parse(tagsJson);
        foreach (var tag in doc.RootElement.GetProperty("data").GetProperty("data").EnumerateArray())
        {
            if (tag.GetProperty("tagName").GetString() == "E2ETag")
            {
                tagId = tag.GetProperty("tagId").GetInt32();
                break;
            }
        }

        Assert.That(tagId, Is.GreaterThan(0), "Should find E2ETag ID");

        var updatePayload = JsonSerializer.Serialize(new { Category = "Updated" });
        var updateResponse = await http.PutAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/tags/{tagId}",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));
        Assert.That((int)updateResponse.StatusCode, Is.LessThan(300),
            $"Update tag should succeed: {updateResponse.StatusCode}");

        var deleteResponse = await http.DeleteAsync($"{TestConstants.ApiBaseUrl}/api/admin/tags/{tagId}");
        Assert.That((int)deleteResponse.StatusCode, Is.LessThan(300),
            $"Delete tag should succeed: {deleteResponse.StatusCode}");

        await NavigateAndWaitAsync("/admin/tags");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var finalContent = await Page.ContentAsync();
        Assert.That(finalContent.Contains("E2ETag"), Is.False, "E2ETag should be deleted");
    }
}
