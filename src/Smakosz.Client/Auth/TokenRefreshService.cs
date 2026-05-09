namespace Smakosz.Client.Auth;

public class TokenRefreshService : ITokenRefreshService
{
    // Named HttpClient without AuthTokenHandler so refresh requests do not recurse.
    public const string RawClientName = "SmakoszAPI-Raw";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public TokenRefreshService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var client = _httpClientFactory.CreateClient(RawClientName);
            using var response = await client.PostAsync("/api/auth/refresh", content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
