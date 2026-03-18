using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T43_AdminSystemConfigTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewAndEditSystemConfig()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/system-config");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/system-config");
        }

        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Konfiguracja systemu");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("moderation.auto_approve_threshold"), Is.True,
            "Should show seeded config key 'moderation.auto_approve_threshold'");
        Assert.That(pageContent.Contains("api.rate_limit_per_minute"), Is.True,
            "Should show seeded config key 'api.rate_limit_per_minute'");

        var configRows = Page.Locator(".row.mb-3.align-items-center");
        var configCount = await configRows.CountAsync();

        for (int i = 0; i < configCount; i++)
        {
            var row = configRows.Nth(i);
            var labelText = await row.Locator("label.form-label").InnerTextAsync();
            if (labelText.Contains("api.rate_limit_per_minute"))
            {
                var input = row.Locator("input.form-control").First;
                await input.ClearAsync();
                await input.FillAsync("120");

                var saveBtn = row.Locator("button.btn-outline-primary").First;
                await saveBtn.ClickAsync();

                await Page.WaitForTimeoutAsync(2000);

                await AssertToastAsync("api.rate_limit_per_minute");
                return;
            }
        }

        Assert.Fail("Could not find 'api.rate_limit_per_minute' config row to edit");
    }
}
