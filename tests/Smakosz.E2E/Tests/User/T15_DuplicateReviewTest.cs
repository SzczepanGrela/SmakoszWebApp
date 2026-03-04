using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T15_DuplicateReviewTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CannotSubmitDuplicateReview_ForSameDish()
    {
        // jan-kowalski already has a review on pizza-margherita (review #1 from seed).
        // Attempting to create another should fail with 409 Conflict.

        using var http = new HttpClient();
        var token = E2EAuthHelper.GenerateUserToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // First get Pizza Margherita's PublicId
        var dishResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/pizza-margherita");
        Assert.That(dishResponse.IsSuccessStatusCode, Is.True,
            $"Failed to get Pizza Margherita: {dishResponse.StatusCode}");

        var dishJson = await dishResponse.Content.ReadAsStringAsync();
        using var dishDoc = JsonDocument.Parse(dishJson);

        Guid dishPublicId;
        if (dishDoc.RootElement.TryGetProperty("data", out var dishData) &&
            dishData.TryGetProperty("publicId", out var dishPidProp))
            dishPublicId = dishPidProp.GetGuid();
        else
            dishPublicId = dishDoc.RootElement.GetProperty("publicId").GetGuid();

        var duplicatePayload = JsonSerializer.Serialize(new
        {
            dishPublicId = dishPublicId,
            dishRating = 7,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "Proba duplikatu recenzji.",
            visitDate = DateTime.Today.ToString("yyyy-MM-dd"),
        });

        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(duplicatePayload, Encoding.UTF8, "application/json"));

        // Error.Conflict maps to 409 in ToErrorResult
        Assert.That((int)response.StatusCode, Is.EqualTo(409),
            $"Expected 409 Conflict for duplicate review. Got: {response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body.ToLower(),
            Does.Contain("recenzj").Or.Contain("review").Or.Contain("duplik").Or.Contain("already").Or.Contain("dodałeś").Or.Contain("dodales"),
            $"Error message should mention duplicate review. Got: {body}");

        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);
        await NavigateAndWaitAsync("/dish/pizza-margherita");
        await WaitForBlazorLoadedAsync();

        var addReviewLink = Page.GetByRole(AriaRole.Link, new() { Name = "Ocen to danie" });
        var linkCount = await addReviewLink.CountAsync();

        if (linkCount == 0)
        {
            Assert.Pass("UI correctly hides 'Ocen to danie' for already-reviewed dish");
        }
    }
}
