using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.CrossRole;

[TestFixture]
public class T10_FullUserJourneyTest : SmakoszE2ETestBase
{
    [Test]
    public async Task NewUser_FullJourney_RegisterBrowseReviewRecommendations()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var username = $"journey{timestamp}";
        var email = $"journey-{timestamp}@test.com";
        var password = "TestHaslo123!";

        await NavigateAndWaitAsync("/register");

        await Page.Locator("input[type='text']").First.FillAsync(username);
        await Page.Locator("input[type='email']").FillAsync(email);
        await Page.Locator(".input-group input[type='password']").FillAsync(password);

        await WaitForTurnstileAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zarejestruj się" }).ClickAsync();

        var redirectTask = Page.WaitForURLAsync(
            url => url.Contains("/verify-email"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var errorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(redirectTask, errorTask);

        if (!Page.Url.Contains("/verify-email"))
        {
            var errorText = await Page.Locator(".alert-danger").First.TextContentAsync();
            Assert.Fail($"Registration failed: {errorText}");
        }

        // Email verification code is logged by StubEmailService - not accessible from E2E.
        // Switch to pre-seeded user with verified email for the rest of the journey.

        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Dan");
        await AssertPageContainsTextAsync("Restauracji");

        await NavigateAndWaitAsync("/restaurants/pizzeria-roma");
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Pizzeria Roma");
        await AssertPageContainsTextAsync("Włoska");

        await NavigateAndWaitAsync("/search?query=kebab");
        await WaitForBlazorLoadedAsync();

        await Page.WaitForTimeoutAsync(2000);

        var bodyText = await Page.Locator("body").InnerTextAsync();
        var hasKebab = bodyText.Contains("Kebab") || bodyText.Contains("Sultan") || bodyText.Contains("kebab");
        Assert.That(hasKebab, Is.True,
            $"Search for 'kebab' should return results. Body (500): {bodyText[..Math.Min(500, bodyText.Length)]}");

        var apiCalls = new List<string>();
        Page.Response += async (_, response) =>
        {
            if (response.Url.Contains("/api/"))
            {
                try
                {
                    var body = await response.TextAsync();
                    apiCalls.Add($"[{response.Status}] {response.Request.Method} {response.Url} -> {body[..Math.Min(500, body.Length)]}");
                }
                catch
                {
                    apiCalls.Add($"[{response.Status}] {response.Request.Method} {response.Url} -> (body unreadable)");
                }
            }
        };

        await NavigateAndWaitAsync("/dishes/tiramisu");
        await WaitForBlazorLoadedAsync();

        var addReviewLink = Page.GetByRole(AriaRole.Link, new() { Name = "Ocen to danie" });
        await Expect(addReviewLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addReviewLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/review/add"), new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Tiramisu");

        var ratingContainers = Page.Locator(".rating-stars-interactive");

        await ratingContainers.Nth(0).Locator("i.interactive-star").Nth(8).ClickAsync();
        await ratingContainers.Nth(1).Locator("i.interactive-star").Nth(7).ClickAsync();
        await ratingContainers.Nth(2).Locator("i.interactive-star").Nth(7).ClickAsync();
        await ratingContainers.Nth(3).Locator("i.interactive-star").Nth(8).ClickAsync();

        await Page.Locator("textarea.form-control").FillAsync(
            "Najlepsze tiramisu jakie jadlem! Test E2E full journey.");

        // Use yesterday to avoid UTC timezone edge case
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
        await Page.Locator("input[type='date']").FillAsync(yesterday);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Opublikuj recenzję" }).ClickAsync();

        var reviewRedirectTask = Page.WaitForURLAsync(
            url => url.Contains("/dishes/tiramisu"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var reviewErrorTask = Page.Locator(".alert-danger, .toast-error").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(reviewRedirectTask, reviewErrorTask);

        if (!Page.Url.Contains("/dishes/tiramisu"))
        {
            var errorText = await Page.Locator(".alert-danger, .toast-error").First.TextContentAsync();
            Assert.Fail($"Review submission failed: {errorText}\nAPI calls:\n{string.Join("\n", apiCalls)}");
        }

        await WaitForBlazorLoadedAsync();
        await AssertToastAsync("Recenzja została opublikowana!");

        await NavigateAndWaitAsync("/recommendations");
        await WaitForBlazorLoadedAsync();

        var recPageContent = await Page.ContentAsync();
        Assert.That(recPageContent, Does.Not.Contain("Unhandled error"),
            "Recommendations page should not crash");
    }
}
