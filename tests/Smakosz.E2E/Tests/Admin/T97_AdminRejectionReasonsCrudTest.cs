using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T97_AdminRejectionReasonsCrudTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanCrudRejectionReasons()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/rejection-reasons");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/rejection-reasons");
        }

        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Powody odrzucenia");
        await AssertPageContainsTextAsync("text_spam");

        using var http = new HttpClient();
        var adminToken = E2EAuthHelper.GenerateAdminToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        const string testCode = "text_e2e_custom";

        var createPayload = JsonSerializer.Serialize(new
        {
            ReasonCode = testCode,
            Category = "Text",
            AdminLabel = "E2E niestandardowy powod",
            UserMessageTemplate = "Recenzja zostala odrzucona z testowego powodu E2E",
            IsActive = true
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/rejection-reasons",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)createResponse.StatusCode, Is.LessThan(300),
            $"Create rejection reason should succeed: {createResponse.StatusCode}");

        var updatePayload = JsonSerializer.Serialize(new
        {
            Category = "Text",
            AdminLabel = "E2E po aktualizacji",
            UserMessageTemplate = "Zaktualizowana tresc komunikatu dla uzytkownika",
            IsActive = false
        });
        var updateResponse = await http.PutAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/rejection-reasons/{testCode}",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));
        Assert.That((int)updateResponse.StatusCode, Is.LessThan(300),
            $"Update rejection reason should succeed: {updateResponse.StatusCode}");

        var listResponse = await http.GetAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/rejection-reasons?includeInactive=true");
        var listJson = await listResponse.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(listJson))
        {
            var found = false;
            foreach (var entry in doc.RootElement.GetProperty("data").GetProperty("data").EnumerateArray())
            {
                if (entry.GetProperty("reasonCode").GetString() == testCode)
                {
                    Assert.That(entry.GetProperty("adminLabel").GetString(), Is.EqualTo("E2E po aktualizacji"));
                    Assert.That(entry.GetProperty("isActive").GetBoolean(), Is.False);
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True, $"Reason {testCode} should be in the list after update");
        }

        var deleteResponse = await http.DeleteAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/rejection-reasons/{testCode}");
        Assert.That((int)deleteResponse.StatusCode, Is.LessThan(300),
            $"Delete rejection reason should succeed: {deleteResponse.StatusCode}");

        var afterDeleteResponse = await http.GetAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/rejection-reasons?includeInactive=true");
        var afterDeleteJson = await afterDeleteResponse.Content.ReadAsStringAsync();
        Assert.That(afterDeleteJson.Contains(testCode), Is.False,
            $"Reason {testCode} should be removed after delete");
    }
}
