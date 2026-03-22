using System.Net.Http.Json;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T91_PublicConfigEndpointTest : SmakoszE2ETestBase
{
    [Test]
    public async Task PublicConfigEndpoint_ReturnsOnlyPublicKeys()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/config/public");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));

        var config = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.That(config, Is.Not.Null);

        Assert.That(config!.ContainsKey("auth.password_min_length"), Is.True,
            "Should contain public key 'auth.password_min_length'");
        Assert.That(config.ContainsKey("upload.max_size_mb"), Is.True,
            "Should contain public key 'upload.max_size_mb'");
        Assert.That(config.ContainsKey("review.min_length"), Is.True,
            "Should contain public key 'review.min_length'");

        Assert.That(config.ContainsKey("retention.system_jobs_days"), Is.False,
            "Should not contain private key 'retention.system_jobs_days'");
        Assert.That(config.ContainsKey("moderation.text_batch_size"), Is.False,
            "Should not contain private key 'moderation.text_batch_size'");
        Assert.That(config.ContainsKey("auth.access_ttl_sec"), Is.False,
            "Should not contain private key 'auth.access_ttl_sec'");
    }
}
