using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Smakosz.Client.Models;

namespace Smakosz.Client.Auth;

public class TokenRefreshService : ITokenRefreshService
{
    // Named HttpClient without AuthTokenHandler so refresh requests do not recurse.
    public const string RawClientName = "SmakoszAPI-Raw";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocalStorageService _localStorage;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public TokenRefreshService(IHttpClientFactory httpClientFactory, ILocalStorageService localStorage)
    {
        _httpClientFactory = httpClientFactory;
        _localStorage = localStorage;
    }

    public async Task<string?> TryRefreshAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var refreshToken = await _localStorage.GetItemAsStringAsync("refresh_token");
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            // Another caller may have already refreshed while we were waiting on the semaphore.
            var currentAccess = await _localStorage.GetItemAsStringAsync("auth_token");
            if (!string.IsNullOrWhiteSpace(currentAccess) && !IsAccessExpired(currentAccess))
                return currentAccess;

            var client = _httpClientFactory.CreateClient(RawClientName);
            using var response = await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = refreshToken },
                JsonOptions,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                await ClearTokensAsync();
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);
            if (envelope is not { Success: true, Data: not null }
                || string.IsNullOrWhiteSpace(envelope.Data.AccessToken)
                || string.IsNullOrWhiteSpace(envelope.Data.RefreshToken))
            {
                await ClearTokensAsync();
                return null;
            }

            await _localStorage.SetItemAsStringAsync("auth_token", envelope.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync("refresh_token", envelope.Data.RefreshToken);
            return envelope.Data.AccessToken;
        }
        catch
        {
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ClearTokensAsync()
    {
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("refresh_token");
    }

    private static bool IsAccessExpired(string token)
    {
        try
        {
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}
