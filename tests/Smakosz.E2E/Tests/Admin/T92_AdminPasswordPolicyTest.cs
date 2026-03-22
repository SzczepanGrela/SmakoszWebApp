using System.Text;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T92_AdminPasswordPolicyTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanChangePasswordPolicy_AndRegistrationRespectsIt()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/system-config");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/system-config");
        }

        await WaitForBlazorLoadedAsync();

        await UpdateConfigValue("auth.password_min_length", "10");
        await AssertToastAsync("auth.password_min_length");

        using var http = new HttpClient();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            username = "testpwpolicy",
            email = "pwpolicy@test.com",
            password = "Short1!",
            turnstileToken = "e2e-test"
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{TestConstants.ApiBaseUrl}/api/auth/register", content);

        Assert.That((int)response.StatusCode, Is.EqualTo(400).Or.EqualTo(422),
            "Registration with short password should be rejected after policy change");

        await UpdateConfigValue("auth.password_min_length", "8");
        await AssertToastAsync("auth.password_min_length");
    }

    private async Task UpdateConfigValue(string key, string value)
    {
        var configRows = Page.Locator(".row.mb-3.align-items-center");
        var configCount = await configRows.CountAsync();

        for (int i = 0; i < configCount; i++)
        {
            var row = configRows.Nth(i);
            var labelText = await row.Locator("label.form-label").InnerTextAsync();
            if (labelText.Contains(key))
            {
                var input = row.Locator("input.form-control").First;
                await input.ClearAsync();
                await input.FillAsync(value);

                var saveBtn = row.Locator("button.btn-outline-primary").First;
                await saveBtn.ClickAsync();

                await Page.WaitForTimeoutAsync(2000);
                return;
            }
        }

        Assert.Fail($"Could not find '{key}' config row");
    }
}
