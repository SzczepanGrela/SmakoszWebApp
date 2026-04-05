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
        Factory = new TestWebApplicationFactory();
        AnonymousClient = Factory.CreateAnonymousClient();
        await SeedAsync();
    }

    protected virtual Task SeedAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        AnonymousClient.Dispose();
        await Factory.DisposeAsync();
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
