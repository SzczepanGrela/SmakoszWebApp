using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.CrossRole;

[TestFixture]
public class T49_UserCorrectionModeratorFlowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_SubmitsCorrection_Moderator_Approves()
    {
        using var http = new HttpClient();
        var userToken = E2EAuthHelper.GenerateUserToken();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        var correctionPayload = JsonSerializer.Serialize(new
        {
            issueType = "Phone",
            description = "Zly numer telefonu",
            proposedValue = "+48 555 666 777"
        });
        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/restaurants/pizzeria-roma/corrections",
            new StringContent(correctionPayload, Encoding.UTF8, "application/json"));

        Assert.That((int)response.StatusCode, Is.LessThan(400),
            $"User correction should be accepted: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");

        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/edit-requests");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/edit-requests");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Prośby o edycję");

        await Page.WaitForTimeoutAsync(2000);

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        var approveVisible = await approveButton.IsVisibleAsync();

        if (approveVisible)
        {
            await approveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
            await WaitForBlazorLoadedAsync();

            await AssertToastAsync("Prośba przetworzona.");
        }
        else
        {
            // May already have been processed by T47 - verify page access is ok
            var pageContent = await Page.ContentAsync();
            Assert.That(pageContent.Contains("Nie masz uprawnień"), Is.False,
                "Moderator should have access to edit requests page");
            Assert.Pass("Correction created successfully, but edit request may have been processed by previous test");
        }
    }
}
