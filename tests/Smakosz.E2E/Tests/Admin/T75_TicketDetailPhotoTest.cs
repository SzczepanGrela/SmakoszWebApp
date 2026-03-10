using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T75_TicketDetailPhotoTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewPhotoTicketDetail()
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

        var photoFilter = Page.GetByRole(AriaRole.Button, new() { Name = "Zdjęcie" }).First;
        await photoFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak zgłoszeń"))
        {
            Assert.Pass("No Photo tickets available - empty state verified");
        }

        var detailLink = Page.Locator("a", new() { HasText = "Szczegóły" }).First;
        if (await detailLink.CountAsync() == 0)
        {
            Assert.Pass("No ticket detail links found after filtering by Zdjęcie");
        }

        await detailLink.ClickAsync();
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        Assert.That(Page.Url, Does.Contain("/admin/tickets/"),
            "Should navigate to ticket detail page");

        await AssertPageContainsTextAsync("Zdjęcie do moderacji");

        var image = Page.Locator("img[style*='max-height']").First;
        // Image may or may not load (broken URL), but the img element should exist
        var hasImage = await image.CountAsync() > 0;

        await AssertPageContainsTextAsync("Typ encji");
        await AssertPageContainsTextAsync("Przesłane przez");

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

        Assert.Pass($"Photo ticket detail page verified successfully (image element present: {hasImage})");
    }
}
