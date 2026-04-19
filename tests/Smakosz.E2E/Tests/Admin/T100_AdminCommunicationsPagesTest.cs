using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T100_AdminCommunicationsPagesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanAccessAllThreeCommunicationLogPages()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/email-logs");
        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/email-logs");
        }
        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Logi email");

        var failedBtn = Page.Locator("button", new() { HasTextString = "Failed" }).First;
        await failedBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        await NavigateAndWaitAsync("/admin/moderation-logs");
        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Logi moderacji");

        await NavigateAndWaitAsync("/admin/ai-logs");
        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Logi AI");
    }
}
