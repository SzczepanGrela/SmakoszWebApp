using System.Net.Http.Headers;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T54_EditReviewUITest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanEditReview_ViaUI()
    {
        using var http = new HttpClient();
        var token = E2EAuthHelper.GenerateUserToken();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var reviewsResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/pizza-margherita/reviews");
        Assert.That(reviewsResponse.IsSuccessStatusCode, Is.True,
            $"Failed to get reviews: {reviewsResponse.StatusCode}");

        var reviewsJson = await reviewsResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(reviewsJson);

        Guid reviewPublicId = Guid.Empty;
        var dataArray = doc.RootElement.TryGetProperty("data", out var dataProp)
            ? (dataProp.ValueKind == JsonValueKind.Array
                ? dataProp
                : dataProp.TryGetProperty("data", out var innerData) ? innerData : dataProp)
            : doc.RootElement;

        if (dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var review in dataArray.EnumerateArray())
            {
                var username = review.TryGetProperty("username", out var uProp)
                    ? uProp.GetString()
                    : review.TryGetProperty("userName", out var u2Prop)
                        ? u2Prop.GetString()
                        : null;

                if (username == "jan-kowalski")
                {
                    reviewPublicId = review.GetProperty("publicId").GetGuid();
                    break;
                }
            }
        }

        if (reviewPublicId == Guid.Empty)
        {
            Assert.Inconclusive("Could not find jan-kowalski's review on pizza-margherita via API. " +
                                $"Response: {reviewsJson[..Math.Min(500, reviewsJson.Length)]}");
        }

        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);
        await NavigateAndWaitAsync($"/review/edit/{reviewPublicId}");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Edytuj recenzję");

        await AssertPageContainsTextAsync("Pizza Margherita");
        await AssertPageContainsTextAsync("Pizzeria Roma");

        var ratingContainers = Page.Locator(".rating-stars-interactive");
        var ratingCount = await ratingContainers.CountAsync();
        Assert.That(ratingCount, Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 rating containers (dish, service, cleanliness, ambiance)");

        var textarea = Page.Locator("textarea.form-control").First;
        await Expect(textarea).ToBeVisibleAsync();
        await textarea.ClearAsync();
        await textarea.FillAsync("Zaktualizowana recenzja z testu E2E T54 - pizza nadal świetna!");

        var dishStars = ratingContainers.First.Locator("i.interactive-star");
        var starCount = await dishStars.CountAsync();
        if (starCount >= 5)
        {
            await dishStars.Nth(4).ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zapisz zmiany" }).ClickAsync();

        var redirectTask = Page.WaitForURLAsync(
            url => url.Contains("/dishes/pizza-margherita"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var toastTask = Page.GetByText("Recenzja została zaktualizowana!").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(redirectTask, toastTask);

        if (Page.Url.Contains("/dishes/pizza-margherita"))
        {
            // Redirected successfully
            await WaitForBlazorLoadedAsync();
            await AssertPageContainsTextAsync("Pizza Margherita");
        }
        else
        {
            // Toast appeared
            await AssertToastAsync("Recenzja została zaktualizowana!");
        }
    }
}
