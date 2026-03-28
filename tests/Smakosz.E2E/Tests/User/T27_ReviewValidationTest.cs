using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T27_ReviewValidationTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_ReviewValidation_RejectsInvalidData()
    {
        await LoginViaLocalStorageAsync(TestConstants.User2Email, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/review/add?dish=pizza-pepperoni");
        await WaitForBlazorLoadedAsync();

        // Try submitting without filling anything
        var submitButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Opublikuj|Dodaj recenzj") }).First;
        if (await submitButton.IsVisibleAsync())
        {
            await submitButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1500);

            // Assert client validation error
            var pageContent = await Page.ContentAsync();
            Assert.That(
                pageContent.Contains("wymagana") || pageContent.Contains("Wymagana") ||
                pageContent.Contains("minimum") || pageContent.Contains("Wypelnij"),
                Is.True,
                "Submitting empty review form should show validation error");
        }

        using var http = new HttpClient();
        var userToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        var shortContentPayload = JsonSerializer.Serialize(new
        {
            dishSlug = "pizza-pepperoni",
            dishRating = 7,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "Short",
            visitDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
        });
        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(shortContentPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)response.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Review with content < 10 chars should be rejected");

        var zeroDishRatingPayload = JsonSerializer.Serialize(new
        {
            dishSlug = "pizza-pepperoni",
            dishRating = 0,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "To jest testowa recenzja z E2E test suite.",
            visitDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
        });
        response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(zeroDishRatingPayload, Encoding.UTF8, "application/json"));
        Assert.That((int)response.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Review with dishRating=0 should be rejected");

        var noVisitDatePayload = JsonSerializer.Serialize(new
        {
            dishSlug = "pizza-pepperoni",
            dishRating = 7,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "To jest testowa recenzja z E2E test suite."
        });
        response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(noVisitDatePayload, Encoding.UTF8, "application/json"));
        Assert.That((int)response.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Review without visitDate should be rejected");
    }
}
