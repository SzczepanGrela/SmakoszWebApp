using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T08_AdminBanUnbanTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanBanAndUnbanUser()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/users");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/users");
        }

        await WaitForBlazorLoadedAsync();

        var heading = Page.Locator("h2").First;
        await Expect(heading).ToContainTextAsync("Użytkownicy",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        await Page.Locator("table.table tbody tr").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        var userRow = Page.Locator("tr", new() { HasText = "anna-nowak" });
        await Expect(userRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        var banButton = userRow.Locator("button.btn-outline-danger").First;
        await banButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        // Use badge locator in HTML (not innerText which may be uppercased by CSS)
        var userRowAfterBan = Page.Locator("tr", new() { HasText = "anna-nowak" });
        var bannedBadge = userRowAfterBan.Locator("span.badge.bg-danger");
        await Expect(bannedBadge).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        var badgeHtml = await bannedBadge.InnerHTMLAsync();
        Assert.That(badgeHtml, Does.Contain("Zbanowany"),
            $"Expected banned badge with 'Zbanowany' text, got: {badgeHtml}");

        var unbanButton = userRowAfterBan.Locator("button.btn-outline-success").First;
        await unbanButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var userRowAfterUnban = Page.Locator("tr", new() { HasText = "anna-nowak" });
        var activeBadge = userRowAfterUnban.Locator("span.badge.bg-success");
        await Expect(activeBadge).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
    }
}
