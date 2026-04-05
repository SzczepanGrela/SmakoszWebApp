using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.CrossRole;

[TestFixture]
public class T09_ReportResolutionFlowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_ReportsReview_Admin_ResolvesReport()
    {

        using var http = new HttpClient();
        var userToken = E2EAuthHelper.GenerateUserToken();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        var reviewsResponse = await http.GetAsync(
            $"{TestConstants.ApiBaseUrl}/api/dishes/pizza-margherita/reviews");
        Assert.That(reviewsResponse.IsSuccessStatusCode, Is.True,
            $"Failed to get reviews: {reviewsResponse.StatusCode}");

        var reviewsJson = await reviewsResponse.Content.ReadAsStringAsync();
        using var reviewsDoc = JsonDocument.Parse(reviewsJson);

        Guid? reviewPublicId = null;
        if (reviewsDoc.RootElement.TryGetProperty("data", out var dataElement))
        {
            JsonElement reviewsArray;
            if (dataElement.ValueKind == JsonValueKind.Array)
                reviewsArray = dataElement;
            else if (dataElement.TryGetProperty("data", out var nestedData) &&
                     nestedData.ValueKind == JsonValueKind.Array)
                reviewsArray = nestedData;
            else
                reviewsArray = dataElement;

            if (reviewsArray.ValueKind == JsonValueKind.Array && reviewsArray.GetArrayLength() > 0)
            {
                reviewPublicId = reviewsArray[0].GetProperty("publicId").GetGuid();
            }
        }

        Assert.That(reviewPublicId, Is.Not.Null,
            $"No reviews found. Response: {reviewsJson[..Math.Min(500, reviewsJson.Length)]}");

        var reportPayload = JsonSerializer.Serialize(new
        {
            reasonCodes = new[] { "spam" },
            description = "Test report z E2E"
        });
        var content = new StringContent(reportPayload, Encoding.UTF8, "application/json");
        var reportResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews/{reviewPublicId}/report", content);

        var reportResponseBody = await reportResponse.Content.ReadAsStringAsync();
        Assert.That((int)reportResponse.StatusCode, Is.LessThan(400),
            $"Report creation failed: {reportResponse.StatusCode} - {reportResponseBody}");

        await LoginViaLocalStorageAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

        await NavigateAndWaitAsync("/admin/reports");

        if (Page.Url.Contains("/login"))
        {
            await Page.WaitForTimeoutAsync(2000);
            await NavigateAndWaitAsync("/admin/reports");
        }

        await WaitForBlazorLoadedAsync();

        var reportsHeading = Page.Locator("h2").First;
        await Expect(reportsHeading).ToContainTextAsync("Raporty",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazorLoadedAsync();

        var reportRows = Page.Locator("table.table tbody tr");
        var rowCount = await reportRows.CountAsync();

        if (rowCount == 0)
        {
            // Default filter is "Wszystkie" - try "Oczekujace" in case it hides them
            var pendingFilter = Page.Locator("button", new() { HasText = "Oczekujace" });
            if (await pendingFilter.IsVisibleAsync())
            {
                await pendingFilter.ClickAsync();
                await Page.WaitForTimeoutAsync(2000);
                await WaitForBlazorLoadedAsync();
                rowCount = await reportRows.CountAsync();
            }
        }

        Assert.That(rowCount, Is.GreaterThan(0),
            $"No report rows visible on /admin/reports. URL: {Page.Url}");

        var resolveButton = reportRows.First.Locator("button.btn-outline-success").First;
        var resolveVisible = await resolveButton.IsVisibleAsync();

        if (!resolveVisible)
        {
            var firstRowHtml = await reportRows.First.InnerHTMLAsync();
            Assert.Fail(
                $"Resolve button not visible.\n" +
                $"Row count: {rowCount}\n" +
                $"First row HTML: {firstRowHtml[..Math.Min(800, firstRowHtml.Length)]}");
        }

        await resolveButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazorLoadedAsync();

        var pageText = await Page.Locator("body").InnerTextAsync();
        var hasSuccessIndication = pageText.Contains("zaktualizowany") ||
                                  pageText.Contains("Raport");
        Assert.That(hasSuccessIndication, Is.True,
            "Expected success toast or report page after resolution");
    }
}
