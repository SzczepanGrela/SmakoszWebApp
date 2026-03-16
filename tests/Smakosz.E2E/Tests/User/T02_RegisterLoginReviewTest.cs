using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T02_RegisterLoginReviewTest : SmakoszE2ETestBase
{
    [Test]
    public async Task NewUser_CanRegister_Login_AndWriteReview()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var username = $"e2etester{timestamp}";
        var email = $"e2e-{timestamp}@test.com";
        var password = "TestHaslo123!";

        await NavigateAndWaitAsync("/register");

        // Fill registration form
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
            Assert.Fail($"Registration failed with error: {errorText}");
        }

        await NavigateAndWaitAsync("/login");

        await Page.Locator("input[type='email']").FillAsync(TestConstants.UserEmail);
        await Page.Locator(".input-group input[type='password']").FillAsync(TestConstants.UserPassword);

        await WaitForTurnstileAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 15_000 });
        await WaitForBlazorLoadedAsync();

        await NavigateAndWaitAsync("/dishes/pizza-pepperoni");

        // Assert "Ocen to danie" button is visible (requires User role)
        var addReviewLink = Page.GetByRole(AriaRole.Link, new() { Name = "Ocen to danie" });
        await Expect(addReviewLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await addReviewLink.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/review/add"), new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        // Assert dish is pre-selected
        await AssertPageContainsTextAsync("Pizza Pepperoni");
        await AssertPageContainsTextAsync("Pizzeria Roma");

        var ratingContainers = Page.Locator(".rating-stars-interactive");

        // Ocena dania: 8th star (index 7)
        await ratingContainers.Nth(0).Locator("i.interactive-star").Nth(7).ClickAsync();

        // Obsluga: 7th star (index 6)
        await ratingContainers.Nth(1).Locator("i.interactive-star").Nth(6).ClickAsync();

        // Czystosc: 8th star (index 7)
        await ratingContainers.Nth(2).Locator("i.interactive-star").Nth(7).ClickAsync();

        // Atmosfera: 7th star (index 6)
        await ratingContainers.Nth(3).Locator("i.interactive-star").Nth(6).ClickAsync();

        // Fill review text
        await Page.Locator("textarea.form-control").FillAsync("Testowa recenzja z testu E2E. Pizza pepperoni byla swietna!");

        // Fill visit date
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        await Page.Locator("input[type='date']").FillAsync(today);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Opublikuj recenzję" }).ClickAsync();

        var reviewRedirectTask = Page.WaitForURLAsync(
            url => url.Contains("/dishes/pizza-pepperoni"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var reviewErrorTask = Page.Locator(".alert-danger, .toast-error").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(reviewRedirectTask, reviewErrorTask);

        if (!Page.Url.Contains("/dishes/pizza-pepperoni"))
        {
            var errorText = await Page.Locator(".alert-danger, .toast-error").First.TextContentAsync();
            Assert.Fail($"Review submission failed: {errorText}");
        }

        await WaitForBlazorLoadedAsync();

        await AssertToastAsync("Recenzja zostala opublikowana!");
    }
}
