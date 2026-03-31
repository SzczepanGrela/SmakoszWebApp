using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.CrossRole;

[TestFixture]
public class T25_BusinessDishUserReviewFlowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task BusinessCreatesDish_UserWritesReview_BusinessSeesIt()
    {

        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/dishes/add");
        await WaitForBlazorLoadedAsync();

        var nameInput = Page.Locator("input[type='text'].form-control").First;
        await nameInput.FillAsync("Panna Cotta");
        await Page.Locator("textarea.form-control").FillAsync("Delikatny wloski deser z wanilia i owocami.");
        await Page.Locator("input[type='number'][step='0.01']").First.FillAsync("18.50");
        var caloriesInput = Page.Locator("input[type='number']:not([step])").First;
        if (!await caloriesInput.IsVisibleAsync())
            caloriesInput = Page.Locator("input[type='number']").Nth(1);
        await caloriesInput.FillAsync("350");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Dodaj danie" }).ClickAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/restaurant/dishes") && !url.Contains("/add"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        await WaitForBlazorLoadedAsync();

        await AssertPageContainsTextAsync("Panna Cotta");

        await Page.EvaluateAsync("localStorage.clear()");
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);

        await NavigateAndWaitAsync("/dishes/panna-cotta");
        await WaitForBlazorLoadedAsync();

        var dishPageContent = await Page.ContentAsync();
        if (!dishPageContent.Contains("Panna Cotta") && !dishPageContent.Contains("panna"))
        {
            // Try search as fallback
            await NavigateAndWaitAsync("/search?query=Panna+Cotta");
            await WaitForBlazorLoadedAsync();
            await Page.WaitForTimeoutAsync(3000);

            var dishLink = Page.GetByText("Panna Cotta").First;
            if (await dishLink.IsVisibleAsync())
            {
                await dishLink.ClickAsync();
                await Page.WaitForURLAsync(url => url.Contains("/dishes/"),
                    new PageWaitForURLOptions { Timeout = 10_000 });
            }
            else
            {
                Assert.Fail("Could not find Panna Cotta dish page via direct navigation or search");
            }
            await WaitForBlazorLoadedAsync();
        }

        var addReviewLink = Page.GetByRole(AriaRole.Link, new() { Name = "Ocen to danie" });
        await Expect(addReviewLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await addReviewLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/review/add"), new PageWaitForURLOptions { Timeout = 10_000 });
        await WaitForBlazorLoadedAsync();

        // Fill review form
        var ratingContainers = Page.Locator(".rating-stars-interactive");
        await ratingContainers.Nth(0).Locator("i.interactive-star").Nth(8).ClickAsync(); // Danie: 9
        await ratingContainers.Nth(1).Locator("i.interactive-star").Nth(7).ClickAsync(); // Obsluga: 8
        await ratingContainers.Nth(2).Locator("i.interactive-star").Nth(8).ClickAsync(); // Czystosc: 9
        await ratingContainers.Nth(3).Locator("i.interactive-star").Nth(7).ClickAsync(); // Atmosfera: 8

        await Page.Locator("textarea.form-control").FillAsync("Panna cotta pyszna, delikatna i kremowa!");
        await Page.Locator("input[type='date']").FillAsync(DateTime.Today.ToString("yyyy-MM-dd"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Opublikuj recenzję" }).ClickAsync();

        var reviewRedirectTask = Page.WaitForURLAsync(
            url => url.Contains("/dishes/panna-cotta"),
            new PageWaitForURLOptions { Timeout = 15_000 });
        var reviewErrorTask = Page.Locator(".alert-danger").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        await Task.WhenAny(reviewRedirectTask, reviewErrorTask);

        if (!Page.Url.Contains("/dishes/"))
        {
            var errorVisible = await Page.Locator(".alert-danger").First.IsVisibleAsync();
            if (errorVisible)
            {
                var errorText = await Page.Locator(".alert-danger").First.TextContentAsync();
                Assert.Fail($"Review submission failed: {errorText}");
            }
        }

        await WaitForBlazorLoadedAsync();

        await Page.EvaluateAsync("localStorage.clear()");
        await LoginViaLocalStorageAsync(TestConstants.BusinessEmail, TestConstants.BusinessPassword);

        await NavigateAndWaitAsync("/restaurant/reviews");
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        // Assert the new review is visible
        var reviewContent = await Page.ContentAsync();
        Assert.That(
            reviewContent.Contains("Panna") || reviewContent.Contains("panna") ||
            reviewContent.Contains("jan-kowalski") || reviewContent.Contains("kremowa"),
            Is.True, "Business owner should see the new review on their restaurant reviews page");
    }
}
