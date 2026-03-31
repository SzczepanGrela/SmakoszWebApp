using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T07_AdminDashboardTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanAccessDashboard_AndSeeStatistics()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin");

        // Admin pages load from AdditionalAssemblies (Smakosz.Client.Ops) - may need extra time
        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("Dashboard",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        await AssertPageContainsTextAsync("Użytkownicy");
        await AssertPageContainsTextAsync("Restauracje");
        await AssertPageContainsTextAsync("Recenzje");

        await AssertPageContainsTextAsync("Oczekujące raporty");
        await AssertPageContainsTextAsync("Oczekujące korekty");
    }
}
