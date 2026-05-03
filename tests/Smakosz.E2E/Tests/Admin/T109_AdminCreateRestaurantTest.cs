using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Admin;

[TestFixture]
public class T109_AdminCreateRestaurantTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Admin_CanCreateRestaurantDirectly_OrphanWithoutOwner()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", E2EAuthHelper.GenerateAdminToken());

        var name = $"E2E Admin Restauracja {Guid.NewGuid():N}".Substring(0, 50);
        var payload = JsonSerializer.Serialize(new
        {
            name,
            address = "ul. Adminska 7",
            cityId = 1,
            cuisineTypeId = 1,
            phone = (string?)null,
            email = (string?)null,
            description = (string?)null,
            ownerId = (int?)null,
            ticketId = (int?)null
        });

        var response = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/restaurants",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Admin restaurant create should succeed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using var conn = new Npgsql.NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT owner_id, is_verified, status FROM restaurants WHERE restaurant_name = @name";
        cmd.Parameters.AddWithValue("name", name);
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.That(await reader.ReadAsync(), Is.True, "Newly created restaurant should be queryable in DB.");
        Assert.That(reader.IsDBNull(0), Is.True, "Owner id should be null when admin creates without owner.");
        Assert.That(reader.GetBoolean(1), Is.True, "Admin-created restaurant should be verified.");
        Assert.That(reader.GetString(2), Is.EqualTo("active"), "Admin-created restaurant should be active.");
    }
}
