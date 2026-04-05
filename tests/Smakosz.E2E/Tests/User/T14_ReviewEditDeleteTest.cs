using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T14_ReviewEditDeleteTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanEditAndDeleteOwnReview_ViaApi()
    {
        // Uses anna-nowak (User2) reviewing kebab-duzy to avoid conflicts
        // with T02 (jan->pepperoni), T10 (jan->tiramisu) and seed data.

        using var http = new HttpClient();
        var token = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // (T64 may have created one earlier in the same test run)
        using (var conn = new Npgsql.NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM reviews WHERE user_id = 2 AND dish_id = (SELECT dish_id FROM dishes WHERE slug = 'kebab-duzy')";
            await cmd.ExecuteNonQueryAsync();
        }

        var dishResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/dishes/kebab-duzy");
        Assert.That(dishResponse.IsSuccessStatusCode, Is.True,
            $"Failed to get Kebab Duzy dish: {dishResponse.StatusCode}");

        var dishJson = await dishResponse.Content.ReadAsStringAsync();
        using var dishDoc = JsonDocument.Parse(dishJson);

        Guid dishPublicId;
        if (dishDoc.RootElement.TryGetProperty("data", out var dishData) &&
            dishData.TryGetProperty("publicId", out var dishPidProp))
            dishPublicId = dishPidProp.GetGuid();
        else
            dishPublicId = dishDoc.RootElement.GetProperty("publicId").GetGuid();

        Assert.That(dishPublicId, Is.Not.EqualTo(Guid.Empty), "Dish PublicId should not be empty");

        var createPayload = JsonSerializer.Serialize(new
        {
            dishPublicId = dishPublicId,
            dishRating = 7,
            serviceRating = 7,
            cleanlinessRating = 7,
            ambianceRating = 7,
            content = "Recenzja kebab do edycji i usuniecia z testu E2E.",
            visitDate = DateTime.Today.ToString("yyyy-MM-dd"),
        });
        var createResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));

        Assert.That(createResponse.IsSuccessStatusCode, Is.True,
            $"Create review failed: {createResponse.StatusCode} - {await createResponse.Content.ReadAsStringAsync()}");

        var createJson = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createJson);

        Guid reviewPublicId;
        if (createDoc.RootElement.TryGetProperty("data", out var dataProp) &&
            dataProp.TryGetProperty("publicId", out var pidProp))
            reviewPublicId = pidProp.GetGuid();
        else
            reviewPublicId = createDoc.RootElement.GetProperty("publicId").GetGuid();

        var editPayload = JsonSerializer.Serialize(new
        {
            dishRating = 9,
            serviceRating = 8,
            cleanlinessRating = 8,
            ambianceRating = 8,
            content = "Zaktualizowana recenzja z testu E2E - kebab fantastyczny!",
            visitDate = DateTime.Today.ToString("yyyy-MM-dd"),
        });
        var editResponse = await http.PutAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews/{reviewPublicId}",
            new StringContent(editPayload, Encoding.UTF8, "application/json"));

        Assert.That(editResponse.IsSuccessStatusCode, Is.True,
            $"Edit review failed: {editResponse.StatusCode} - {await editResponse.Content.ReadAsStringAsync()}");

        await LoginViaLocalStorageAsync(TestConstants.User2Email, TestConstants.UserPassword);
        await NavigateAndWaitAsync("/dishes/kebab-duzy");
        await WaitForBlazorLoadedAsync();

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent.Contains("error") && pageContent.Contains("500"), Is.False,
            "Dish page should load without server errors");

        var deleteResponse = await http.DeleteAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews/{reviewPublicId}");

        Assert.That(deleteResponse.IsSuccessStatusCode, Is.True,
            $"Delete review failed: {deleteResponse.StatusCode} - {await deleteResponse.Content.ReadAsStringAsync()}");

        var secondDeleteResponse = await http.DeleteAsync(
            $"{TestConstants.ApiBaseUrl}/api/reviews/{reviewPublicId}");
        Assert.That((int)secondDeleteResponse.StatusCode, Is.Not.EqualTo(200).And.Not.EqualTo(204),
            "Second delete of same review should fail (already deleted)");
    }
}
