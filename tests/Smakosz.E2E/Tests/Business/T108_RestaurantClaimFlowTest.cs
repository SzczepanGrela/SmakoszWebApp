using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Business;

[TestFixture]
public class T108_RestaurantClaimFlowTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanClaimOrphanRestaurant_AndAdminApprovesPromotingToRestaurantRole()
    {
        using var http = new HttpClient();
        var userToken = E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var lookup = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/restaurants/sultan-kebab");
        Assert.That(lookup.IsSuccessStatusCode, Is.True,
            $"Restaurant lookup should succeed: {lookup.StatusCode}");
        using var lookupDoc = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        var publicId = lookupDoc.RootElement.GetProperty("data").GetProperty("publicId").GetGuid();

        var claimPayload = JsonSerializer.Serialize(new
        {
            justification = "Jestem wlascicielem Sultan Kebab od 2018 roku. Mam dokumenty NIP i REGON do okazania."
        });
        var claimResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/restaurants/{publicId}/claim",
            new StringContent(claimPayload, Encoding.UTF8, "application/json"));

        Assert.That(claimResponse.IsSuccessStatusCode, Is.True,
            $"Claim creation should succeed: {claimResponse.StatusCode}: {await claimResponse.Content.ReadAsStringAsync()}");

        int ticketId;
        using (var conn = new Npgsql.NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ticket_id FROM system.tickets
                WHERE ticket_type = 'restaurant_claim'
                  AND requester_id = 2
                  AND status = 'open'
                ORDER BY ticket_id DESC LIMIT 1";
            var raw = await cmd.ExecuteScalarAsync();
            Assert.That(raw, Is.Not.Null, "Claim ticket should exist in DB after POST claim.");
            ticketId = (int)raw!;
        }

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", E2EAuthHelper.GenerateAdminToken());

        var approveResponse = await http.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/admin/tickets/{ticketId}/approve-claim",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.That(approveResponse.IsSuccessStatusCode, Is.True,
            $"Admin approve-claim should succeed: {approveResponse.StatusCode}: {await approveResponse.Content.ReadAsStringAsync()}");

        using (var conn = new Npgsql.NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();

            using var ticketCmd = conn.CreateCommand();
            ticketCmd.CommandText = "SELECT status, resolved_by_admin_id FROM system.tickets WHERE ticket_id = @id";
            ticketCmd.Parameters.AddWithValue("id", ticketId);
            using (var reader = await ticketCmd.ExecuteReaderAsync())
            {
                Assert.That(await reader.ReadAsync(), Is.True);
                Assert.That(reader.GetString(0), Is.EqualTo("resolved"));
                Assert.That(reader.IsDBNull(1), Is.False);
            }

            using var restaurantCmd = conn.CreateCommand();
            restaurantCmd.CommandText = "SELECT owner_id, is_verified, status FROM restaurants WHERE slug = 'sultan-kebab'";
            using (var reader = await restaurantCmd.ExecuteReaderAsync())
            {
                Assert.That(await reader.ReadAsync(), Is.True);
                Assert.That(reader.GetInt32(0), Is.EqualTo(2));
                Assert.That(reader.GetBoolean(1), Is.True);
                Assert.That(reader.GetString(2), Is.EqualTo("active"));
            }

            using var roleCmd = conn.CreateCommand();
            roleCmd.CommandText = "SELECT role FROM users WHERE user_id = 2";
            var role = (string)(await roleCmd.ExecuteScalarAsync())!;
            Assert.That(role, Is.EqualTo("restaurant"),
                "User role should be promoted to Restaurant after claim approval.");
        }
    }
}
