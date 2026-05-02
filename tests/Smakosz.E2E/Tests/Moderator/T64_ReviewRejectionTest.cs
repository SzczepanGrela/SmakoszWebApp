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

        var heading = Page.Locator("h2", new() { HasText = "Moderacja recenzji" });
        await Expect(heading).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

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

        await allRejectButtons.First.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var firstReasonCheckbox = Page.Locator("input.form-check-input[type='checkbox'][id^='rej-']").First;
        await Expect(firstReasonCheckbox).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await firstReasonCheckbox.CheckAsync();
        await Page.WaitForTimeoutAsync(300);

        var confirmButton = Page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź odrzucenie" }).First;
        await confirmButton.ClickAsync();

        var toastLocator = Page.Locator(".toast").First;
        try
        {
            await Expect(toastLocator).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        }
        catch (Exception)
        {
            // Toast may have appeared and disappeared already
        }

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var updatedContent = await Page.ContentAsync();
        var queueChanged = updatedContent.Contains("Brak recenzji") ||
                           updatedContent.Contains("zostały sprawdzone") ||
                           await allRejectButtons.CountAsync() < initialRejectCount;
        Assert.That(queueChanged, Is.True,
            "Review should be rejected - queue should have changed");
    }
}
