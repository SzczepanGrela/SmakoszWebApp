using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Metrics;

public class MetricsEndpointTests : IAsyncLifetime
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await PostgresFixture.EnsureStartedAsync();
        _factory = new TestWebApplicationFactory(PostgresFixture.ConnectionString);
        _client = _factory.CreateAnonymousClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await PostgresFixture.ResetAsync();
    }

    [Fact]
    public async Task GetMetrics_ReturnsOk_WithPrometheusFormat()
    {
        var response = await _client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("text/plain");
        body.Should().Contain("# HELP");
        body.Should().Contain("process_cpu_seconds_total");
    }

    [Fact]
    public async Task GetMetrics_NoAuthRequired_Returns200()
    {
        var response = await _client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HttpMetrics_AfterRequest_CounterIncremented()
    {
        await _client.GetAsync("/health/live");
        await _client.GetAsync("/health/live");

        var response = await _client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("http_requests_received_total");
    }
}
