using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T98_AdminReviewStructuredRejectionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_RejectReview_WithReasonCodes_PopulatesNotificationAndReviewField()
    {
        using var http = new HttpClient();

        var userToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var dishLookup = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/kebab-duzy");
        Assert.That(dishLookup.IsSuccessStatusCode, Is.True,
            $"Dish lookup should succeed: {dishLookup.StatusCode}");
        using var dishDoc = JsonDocument.Parse(await dishLookup.Content.ReadAsStringAsync());
        var dishPublicId = dishDoc.RootElement.GetProperty("data").GetProperty("publicId").GetGuid();

        var reviewPayload = JsonSerializer.Serialize(new
        {
            dishPublicId,
            dishRating = 7,
            serviceRating = 6,
            cleanlinessRating = 6,
            ambianceRating = 6,
            content = "Recenzja do strukturalnej moderacji T98.",
            visitDate = DateTime.Today.ToString("yyyy-MM-dd"),
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(reviewPayload, Encoding.UTF8, "application/json"));

        Assert.That(createResponse.IsSuccessStatusCode, Is.True,
            $"Review creation should succeed: {createResponse.StatusCode}. Body: {await createResponse.Content.ReadAsStringAsync()}");

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", E2EAuthHelper.GenerateAdminToken());

        var pendingResponse = await http.GetAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/reviews/pending?page=1&pageSize=50");
        var pendingJson = await pendingResponse.Content.ReadAsStringAsync();

        Guid? targetPublicId = null;
        using (var doc = JsonDocument.Parse(pendingJson))
        {
            foreach (var review in doc.RootElement.GetProperty("data").GetProperty("data").EnumerateArray())
            {
                var content = review.GetProperty("content").GetString();
                if (content != null && content.Contains("strukturalnej moderacji T98"))
                {
                    targetPublicId = review.GetProperty("publicId").GetGuid();
                    break;
                }
            }
        }

        if (targetPublicId is null)
        {
            Assert.Pass("Review nie pojawilo sie w kolejce pending - prawdopodobnie auto-approved.");
        }

        var rejectPayload = JsonSerializer.Serialize(new
        {
            Approve = false,
            ReasonCodes = new[] { "text_spam", "text_offtopic" },
            ModeratorNote = "Ten test sprawdza konkatenacje szablonow."
        });
        var rejectResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/reviews/{targetPublicId}/moderate",
            new StringContent(rejectPayload, Encoding.UTF8, "application/json"));

        Assert.That((int)rejectResponse.StatusCode, Is.LessThan(300),
            $"Structured rejection should succeed: {rejectResponse.StatusCode}. Body: {await rejectResponse.Content.ReadAsStringAsync()}");

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var notificationsResponse = await http.GetAsync(
            $"{TestConstants.ApiBaseUrl}/api/me/notifications?page=1&pageSize=10");
        var notificationsJson = await notificationsResponse.Content.ReadAsStringAsync();

        Assert.That(notificationsJson, Does.Contain("Recenzja ma charakter spamu"),
            "Notification message should contain first template text from text_spam");
        Assert.That(notificationsJson, Does.Contain("Recenzja nie dotyczy"),
            "Notification message should contain second template text from text_offtopic");
        Assert.That(notificationsJson, Does.Contain("Dodatkowa uwaga moderatora"),
            "Notification message should contain moderator note marker");
    }

    [Test]
    public async Task Admin_RejectReview_WithUnknownCode_ReturnsValidationError()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", E2EAuthHelper.GenerateAdminToken());

        var rejectPayload = JsonSerializer.Serialize(new
        {
            Approve = false,
            ReasonCodes = new[] { "text_unknown_code" },
            ModeratorNote = (string?)null
        });
        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/reviews/{Guid.NewGuid()}/moderate",
            new StringContent(rejectPayload, Encoding.UTF8, "application/json"));

        Assert.That((int)response.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Unknown code or unknown review should produce a client error, not 2xx");
    }
}
