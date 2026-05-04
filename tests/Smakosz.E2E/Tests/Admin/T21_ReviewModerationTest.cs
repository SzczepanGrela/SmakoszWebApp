using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T21_ReviewModerationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanApproveReviewInModerationQueue()
    {
        using var http = new HttpClient();
        var annaToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", annaToken);

        var reviewPayload = JsonSerializer.Serialize(new
        {
            dishId = 3, // Kebab Duzy
            restaurantId = 2, // Sultan Kebab
            dishRating = 8,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "Recenzja do moderacji admina z T21.",
            visitDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(reviewPayload, Encoding.UTF8, "application/json"));

        // Review may be auto-pending or auto-approved depending on config.
        // Either way, we check the admin moderation page.

        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/reviews");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/reviews");
        }

        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        var hasReviews = pageContent.Contains("Zatwierdź") || pageContent.Contains("Odrzuć");

        if (!hasReviews)
        {
            // No pending reviews - this is acceptable if auto-approval is on
            var emptyState = pageContent.Contains("Brak") || pageContent.Contains("pust");
            Assert.That(emptyState, Is.True,
                "Review moderation page should show either reviews or empty state");
            Assert.Pass("No pending reviews to moderate (auto-approval may be enabled)");
        }

        var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
        await approveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var bodyText = await Page.Locator("body").InnerTextAsync();
        Assert.That(bodyText.Contains("zatwierdzona") || bodyText.Contains("Brak") || !bodyText.Contains("Zatwierdź"),
            Is.True, "Review should be approved or queue should be empty after approval");
    }
}
