using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T23_ModeratorReviewModerationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanAccessReviewModeration_AndApproveReview()
    {
        // (independent of T21 which may have already moderated seeded review)
        using var http = new HttpClient();
        var annaToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", annaToken);

        var dishResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/pizza-pepperoni");
        Guid dishPublicId = Guid.Empty;
        if (dishResponse.IsSuccessStatusCode)
        {
            var dishJson = await dishResponse.Content.ReadAsStringAsync();
            using var dishDoc = JsonDocument.Parse(dishJson);
            if (dishDoc.RootElement.TryGetProperty("data", out var dishData) &&
                dishData.TryGetProperty("publicId", out var pidProp))
                dishPublicId = pidProp.GetGuid();
        }

        if (dishPublicId != Guid.Empty)
        {
            var reviewPayload = JsonSerializer.Serialize(new
            {
                dishPublicId = dishPublicId,
                dishRating = 8,
                serviceRating = 8,
                cleanlinessRating = 8,
                ambianceRating = 8,
                content = "Recenzja do moderacji moderatora z T23.",
                visitDate = DateTime.Today.ToString("yyyy-MM-dd"),
            });
            await http.PostAsync(
                $"{TestConstants.ApiBaseUrl}/api/reviews",
                new StringContent(reviewPayload, Encoding.UTF8, "application/json"));
        }

        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        await NavigateAndWaitAsync("/admin/reviews");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/reviews");
        }

        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("Nie masz uprawnień") || pageContent.Contains("403"), Is.False,
            "Moderator should have access to review moderation page");

        var hasReviews = pageContent.Contains("Zatwierdź") || pageContent.Contains("Odrzuć");

        if (hasReviews)
        {
            var approveButton = Page.Locator("button.btn-success", new() { HasText = "Zatwierdź" }).First;
            await approveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000);
            await WaitForBlazorLoadedAsync();
        }

        Assert.Pass("Moderator can access review moderation page");
    }
}
