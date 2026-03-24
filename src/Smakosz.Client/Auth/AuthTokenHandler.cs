using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace Smakosz.Client.Auth;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private bool _isRefreshing;

    public AuthTokenHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsStringAsync("auth_token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            _isRefreshing = true;
            try
            {
                var refreshToken = await _localStorage.GetItemAsStringAsync("refresh_token");
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
                    {
                        Content = JsonContent.Create(new { RefreshToken = refreshToken })
                    };

                    var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        var result = await refreshResponse.Content.ReadFromJsonAsync<RefreshResult>(cancellationToken: cancellationToken);
                        if (result != null)
                        {
                            await _localStorage.SetItemAsStringAsync("auth_token", result.AccessToken);
                            await _localStorage.SetItemAsStringAsync("refresh_token", result.RefreshToken);

                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                            response = await base.SendAsync(request, cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        return response;
    }

    private record RefreshResult(string AccessToken, string RefreshToken);
}
