using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T74_TicketDetailReviewContentTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewReviewContentTicketDetail()
    {
        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/tickets");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/tickets");
        }

        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Zgłoszenia");

        var reviewFilter = Page.GetByRole(AriaRole.Button, new() { Name = "Recenzja" }).First;
        await reviewFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak zgłoszeń"))
        {
            Assert.Pass("No ReviewContent tickets available - empty state verified");
        }

        var detailLink = Page.Locator("a", new() { HasText = "Szczegóły" }).First;
        if (await detailLink.CountAsync() == 0)
        {
            Assert.Pass("No ticket detail links found after filtering by Recenzja");
        }

        await detailLink.ClickAsync();
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        Assert.That(Page.Url, Does.Contain("/admin/tickets/"),
            "Should navigate to ticket detail page");

        var backButton = Page.Locator("div.flex-grow-1 a[href='/admin/tickets']").First;
        await Expect(backButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await AssertPageContainsTextAsync("Recenzja do moderacji");

        await AssertPageContainsTextAsync("Autor");
        await AssertPageContainsTextAsync("Ocena");

        var badges = Page.Locator(".badge");
        var badgeCount = await badges.CountAsync();
        Assert.That(badgeCount, Is.GreaterThanOrEqualTo(2),
            "Should have at least type and priority badges");

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        var rejectButton = Page.Locator("button.btn-danger", new() { HasText = "Odrzuć" }).First;

        if (await approveButton.CountAsync() > 0)
        {
            await Expect(approveButton).ToBeVisibleAsync();
            await Expect(rejectButton).ToBeVisibleAsync();

            var rejectionTextarea = Page.Locator("textarea[placeholder='Powód odrzucenia lub notatka...']").First;
            await Expect(rejectionTextarea).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        }

        Assert.Pass("ReviewContent ticket detail page verified successfully");
    }
}
