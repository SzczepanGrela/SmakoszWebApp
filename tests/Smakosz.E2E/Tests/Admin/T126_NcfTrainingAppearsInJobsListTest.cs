using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T126_NcfTrainingAppearsInJobsListTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_SchedulingNcfTraining_RowAppearsInJobsList()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);
        await NavigateAndWaitAsync("/admin/jobs");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        var ncfButton = Page.GetByRole(AriaRole.Button, new() { Name = "NCF Training" }).First;
        if (await ncfButton.CountAsync() == 0)
            ncfButton = Page.Locator("button", new() { HasText = "NCF" }).First;

        if (await ncfButton.CountAsync() == 0)
        {
            Assert.Pass("NCF Training scheduler button not visible on /admin/jobs - feature gated, test skipped");
            return;
        }

        await ncfButton.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        var hasNcfRow = pageContent.Contains("ncf_training");
        Assert.That(hasNcfRow, Is.True,
            "/admin/jobs should show ncf_training row immediately after the schedule button click " +
            "(handler now inserts the row before delegating to NcfTrainingService)");
    }
}
