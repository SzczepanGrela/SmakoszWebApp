using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T57_BusinessRegistrationFlowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanRequestNewRestaurantViaTicketFlow()
    {
        using var http = new HttpClient();
        var token = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = JsonSerializer.Serialize(new
        {
            name = "E2E Testowa Restauracja",
            address = "ul. Testowa 1, Warszawa",
            phone = "+48 111 222 333",
            email = "kontakt@e2e-test.pl",
            description = "Restauracja zgloszona w tescie E2E.",
            cityId = (int?)null,
            cuisineTypeId = (int?)null
        });
        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/restaurants/request",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Request endpoint should accept the payload. Got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using (var conn = new Npgsql.NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM system.tickets
                WHERE ticket_type = 'restaurant_request'
                  AND requester_id = 2
                  AND status = 'open'";
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.That(count, Is.GreaterThanOrEqualTo(1),
                "A RestaurantRequest ticket should exist for the user after submitting the form.");
        }

        var duplicate = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/restaurants/request",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.That((int)duplicate.StatusCode, Is.EqualTo(409),
            "A second pending request from the same user should be rejected with 409.");
    }
}
