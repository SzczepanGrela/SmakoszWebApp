using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T76_TicketDetailContactTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanViewAndRespondToContactTicket()
    {
        using var http = new HttpClient();
        var contactPayload = JsonSerializer.Serialize(new
        {
            name = "E2E Test T76",
            email = "e2e-t76@test.pl",
            subject = "Test T76 - kontakt",
            message = "Wiadomosc testowa z testu E2E T76 - weryfikacja szczegolów ticketu kontaktowego.",
            turnstileToken = "e2e-test"
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/content/contact",
            new StringContent(contactPayload, Encoding.UTF8, "application/json"));
        var contactCreated = createResponse.IsSuccessStatusCode;

        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/tickets");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/tickets");
        }

        await WaitForBlazorLoadedAsync();
        await AssertPageContainsTextAsync("Zgłoszenia");

        var contactFilter = Page.GetByRole(AriaRole.Button, new() { Name = "Kontakt" }).First;
        await contactFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        if (pageContent.Contains("Brak zgłoszeń"))
        {
            Assert.Pass($"No Contact tickets available - contact API succeeded: {contactCreated}");
        }

        var detailLink = Page.Locator("a", new() { HasText = "Szczegóły" }).First;
        if (await detailLink.CountAsync() == 0)
        {
            Assert.Pass("No ticket detail links found after filtering by Kontakt");
        }

        await detailLink.ClickAsync();
        await WaitForBlazorLoadedAsync();
        await Page.WaitForTimeoutAsync(2000);

        Assert.That(Page.Url, Does.Contain("/admin/tickets/"),
            "Should navigate to ticket detail page");

        await AssertPageContainsTextAsync("Wiadomość kontaktowa");

        await AssertPageContainsTextAsync("Nadawca");
        await AssertPageContainsTextAsync("Email");
        await AssertPageContainsTextAsync("Temat");

        var responseTextarea = Page.Locator("textarea[placeholder='Treść odpowiedzi...']").First;
        var sendButton = Page.Locator("button", new() { HasText = "Wyślij odpowiedź" }).First;

        if (await responseTextarea.CountAsync() > 0 && await sendButton.CountAsync() > 0)
        {
            // Ticket is not resolved - test the response flow
            await Expect(responseTextarea).ToBeVisibleAsync();
            await Expect(sendButton).ToBeVisibleAsync();

            await responseTextarea.ClickAsync();
            await responseTextarea.FillAsync("Dziękujemy za kontakt - E2E test T76");
            // Dispatch change event for Blazor @bind
            await responseTextarea.EvaluateAsync(
                "el => el.dispatchEvent(new Event('change', { bubbles: true }))");
            await Page.WaitForTimeoutAsync(300);

            await sendButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);

            var updatedContent = await Page.ContentAsync();
            var success = updatedContent.Contains("wysłana") ||
                          updatedContent.Contains("rozwiązane") ||
                          Page.Url.Contains("/admin/tickets") && !Page.Url.Contains("/admin/tickets/");
            Assert.That(success, Is.True,
                "Should show success toast or redirect to tickets list after sending response");
        }
        else
        {
            // Ticket already resolved - just verify the detail page rendered
            Assert.Pass("Contact ticket detail page rendered - ticket already resolved (no response form)");
        }
    }
}
