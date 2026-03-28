using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.RoleAccess;

public class WorkerAccessTests : IntegrationTestBase
{
    [Fact]
    public async Task Config_ValidApiKey_Returns200()
    {
        using var client = Factory.CreateWorkerClient();

        var response = await client.GetAsync("/api/worker/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Config_InvalidApiKey_Returns401()
    {
        using var client = Factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-key");
        client.DefaultRequestHeaders.Add("X-Worker-Id", "test-worker");

        var response = await client.GetAsync("/api/worker/config");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Config_WithJwt_Returns401()
    {
        // JWT auth should not work for worker endpoints
        using var client = Factory.CreateAdminClient();

        var response = await client.GetAsync("/api/worker/config");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Heartbeat_ValidKey_Returns204()
    {
        using var client = Factory.CreateWorkerClient();

        var response = await client.PostAsJsonAsync("/api/worker/heartbeat", new
        {
            NodeId = "test-node-1",
            IpAddress = "192.168.1.100",
            GpuName = "NVIDIA RTX 4090",
            GpuMemoryTotal = 24576,
            GpuMemoryUsed = 0
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }
}
