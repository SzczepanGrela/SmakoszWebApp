using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T93_AdminUploadLimitsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanChangeUploadLimits_AndValuePersistsAfterReload()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/system-config");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/system-config");
        }

        await WaitForBlazorLoadedAsync();

        var currentValue = await GetConfigValue("upload.max_size_mb");
        Assert.That(currentValue, Is.EqualTo("5"), "Default upload.max_size_mb should be 5");

        await UpdateConfigValue("upload.max_size_mb", "2");
        await AssertToastAsync("upload.max_size_mb");

        await NavigateAndWaitAsync("/admin/system-config");
        await WaitForBlazorLoadedAsync();

        var newValue = await GetConfigValue("upload.max_size_mb");
        Assert.That(newValue, Is.EqualTo("2"), "Value should persist as '2' after reload");

        await UpdateConfigValue("upload.max_size_mb", "5");
        await AssertToastAsync("upload.max_size_mb");
    }

    private async Task<string?> GetConfigValue(string key)
    {
        var configRows = Page.Locator(".row.mb-3.align-items-center");
        var configCount = await configRows.CountAsync();

        for (int i = 0; i < configCount; i++)
        {
            var row = configRows.Nth(i);
            var labelText = await row.Locator("label.form-label").InnerTextAsync();
            if (labelText.Contains(key))
            {
                return await row.Locator("input.form-control").First.InputValueAsync();
            }
        }

        Assert.Fail($"Could not find '{key}' config row");
        return null;
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
