using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Smakosz.Client.Auth;

public class CookieAuthStateProvider : AuthenticationStateProvider
{
    // Named HttpClient without RefreshOn401Handler so /me does not recurse into refresh logic.
    public const string RawClientName = "SmakoszAPI-Raw";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    private ClaimsPrincipal? _cached;

    public CookieAuthStateProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null)
            return new AuthenticationState(_cached);

        var principal = await FetchMeAsync();
        _cached = principal;
        return new AuthenticationState(principal);
    }

    public async Task NotifyUserAuthenticationAsync()
    {
        _cached = await FetchMeAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_cached)));
    }

    public void NotifyUserLogout()
    {
        _cached = _anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    private async Task<ClaimsPrincipal> FetchMeAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient(RawClientName);
            using var response = await client.GetAsync("/api/auth/me");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return _anonymous;
            if (!response.IsSuccessStatusCode)
                return _anonymous;

            var envelope = await response.Content.ReadFromJsonAsync<MeEnvelope>(JsonOptions);
            if (envelope?.Data is null)
                return _anonymous;

            var data = envelope.Data;
            var claims = new List<Claim>();
            if (!string.IsNullOrEmpty(data.UserId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, data.UserId));
            if (!string.IsNullOrEmpty(data.Username))
                claims.Add(new Claim(ClaimTypes.Name, data.Username));
            if (!string.IsNullOrEmpty(data.Email))
                claims.Add(new Claim(ClaimTypes.Email, data.Email));
            if (!string.IsNullOrEmpty(data.Role))
                claims.Add(new Claim(ClaimTypes.Role, data.Role));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }
        catch
        {
            return _anonymous;
        }
    }

    private sealed class MeEnvelope
    {
        public bool Success { get; set; }
        public MeData? Data { get; set; }
    }

    private sealed class MeData
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
