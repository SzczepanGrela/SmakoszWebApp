using System.Text.Json;
using Smakosz.API.Common;

namespace Smakosz.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected TestWebApplicationFactory Factory = null!;
    protected HttpClient AnonymousClient = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public virtual async Task InitializeAsync()
    {
        await PostgresFixture.EnsureStartedAsync();
        Factory = new TestWebApplicationFactory(PostgresFixture.ConnectionString);
        AnonymousClient = Factory.CreateAnonymousClient();
        await Factory.SeedDataAsync(SeedHelpers.SeedFkDefaultsAsync);
        await SeedAsync();
        // Tests insert rows with explicit primary key values, so the underlying SERIAL/IDENTITY sequence still points at 1; the next API-driven insert without an explicit id collides with the seed row unless we bump every sequence past its current max.
        await PostgresFixture.AdvanceSequencesAsync();
    }

    protected virtual Task SeedAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        AnonymousClient.Dispose();
        await Factory.DisposeAsync();
        await PostgresFixture.ResetAsync();
    }

    protected static async Task<T?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
        return apiResponse is { Success: true } ? apiResponse.Data : default;
    }

    protected static async Task<ApiError?> DeserializeError(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(content, JsonOptions);
        return apiResponse?.Error;
    }
}
