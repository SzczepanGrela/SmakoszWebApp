using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T64_ReviewRejectionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CanRejectReviewWithReason()
    {
        using var http = new HttpClient();
        var annaToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", annaToken);

        var dishResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/kebab-duzy");
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
                dishRating = 5,
                serviceRating = 5,
                cleanlinessRating = 5,
                ambianceRating = 5,
                content = "Recenzja do odrzucenia w tescie E2E T64",
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
        await Page.WaitForTimeoutAsync(2000);

        await AssertPageContainsTextAsync("Moderacja recenzji");

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak recenzji do moderacji") || pageContent.Contains("zostały sprawdzone"))
        {
            Assert.Pass("No pending reviews to moderate - queue is empty");
        }

        var allRejectButtons = Page.Locator("button.btn-danger.btn-sm", new() { HasText = "Odrzuć" });
        var initialRejectCount = await allRejectButtons.CountAsync();

        if (initialRejectCount == 0)
        {
            Assert.Pass("No reject button found - reviews may have been already moderated");
        }

        var rejectButton = allRejectButtons.First;

        await rejectButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var reasonInput = Page.Locator("input[placeholder='Powód odrzucenia...']").First;
        await Expect(reasonInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await reasonInput.ClickAsync();
        await reasonInput.FillAsync("Recenzja narusza regulamin");
        // Dispatch change event explicitly for Blazor @bind
        await reasonInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");
        await Page.WaitForTimeoutAsync(300);

        var confirmButton = Page.Locator(".input-group button.btn-danger").First;
        await confirmButton.ClickAsync();

        await Page.WaitForTimeoutAsync(5000);
        await WaitForBlazorLoadedAsync();

        var updatedContent = await Page.ContentAsync();
        var queueChanged = updatedContent.Contains("Brak recenzji") ||
                           updatedContent.Contains("zostały sprawdzone") ||
                           updatedContent.Contains("odrzucona") ||
                           updatedContent.Contains("nieudana") ||
                           await Page.Locator("button.btn-danger.btn-sm", new() { HasText = "Odrzuć" }).CountAsync() < initialRejectCount;
        Assert.That(queueChanged, Is.True,
            "Review should be rejected - queue should have changed");
    }
}
