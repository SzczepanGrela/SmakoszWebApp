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
        Assert.That(pageContent.Contains("moderation.text_batch_size"), Is.True,
            "Should show seeded config key 'moderation.text_batch_size'");
        Assert.That(pageContent.Contains("auth.access_ttl_sec"), Is.True,
            "Should show seeded config key 'auth.access_ttl_sec'");

        var configRows = Page.Locator(".row.mb-3.align-items-center");
        var configCount = await configRows.CountAsync();

        for (int i = 0; i < configCount; i++)
        {
            var row = configRows.Nth(i);
            var labelText = await row.Locator("label.form-label").InnerTextAsync();
            if (labelText.Contains("auth.access_ttl_sec"))
            {
                var input = row.Locator("input.form-control").First;
                await input.ClearAsync();
                await input.FillAsync("600");

                var saveBtn = row.Locator("button.btn-outline-primary").First;
                await saveBtn.ClickAsync();

                await Page.WaitForTimeoutAsync(2000);

                await AssertToastAsync("auth.access_ttl_sec");
                return;
            }
        }

        Assert.Fail("Could not find 'auth.access_ttl_sec' config row to edit");
    }
}
