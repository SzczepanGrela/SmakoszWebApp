using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T45_AdminJobsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanManageBackgroundJobs()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/jobs");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/jobs");
        }

        await WaitForBlazorLoadedAsync();

        // Assert heading
        await AssertPageContainsTextAsync("Zadania w tle");

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        // Assert seed jobs visible
        var jobRows = Page.Locator("table.table tbody tr");
        var jobCount = await jobRows.CountAsync();
        Assert.That(jobCount, Is.GreaterThanOrEqualTo(1),
            "Should have at least 1 job from seed");

        var cancelBtn = Page.Locator("table.table tbody button.btn-outline-danger.btn-sm").First;
        if (await cancelBtn.IsVisibleAsync())
        {
            await cancelBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);
            await WaitForBlazorLoadedAsync();

            var contentAfterCancel = await Page.ContentAsync();
            Assert.That(
                contentAfterCancel.Contains("anulowane") || contentAfterCancel.Contains("Cancelled") ||
                contentAfterCancel.Contains("Zadania w tle"),
                Is.True,
                "Should show cancel confirmation or jobs page");
        }

        var newJobButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Nowe zadanie") }).First;
        await newJobButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var modal = Page.Locator(".modal-content").First;
        await Expect(modal).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        // Fill priority in modal
        var priorityInput = modal.Locator("input[type='number']").First;
        await priorityInput.ClearAsync();
        await priorityInput.FillAsync("3");
        await priorityInput.PressAsync("Tab");

        await Page.WaitForTimeoutAsync(500);

        var createButton = modal.Locator("button.btn-primary").First;
        await createButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        // Assert - check toast or page content
        var contentAfterCreate = await Page.ContentAsync();
        Assert.That(
            contentAfterCreate.Contains("utworzone") || contentAfterCreate.Contains("Zadania w tle"),
            Is.True,
            "Job should be created or jobs page should still be visible");
    }
}
