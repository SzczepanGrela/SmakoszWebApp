using System.Net;
using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace Smakosz.Client.Auth;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly ITokenRefreshService _refresh;

    public AuthTokenHandler(ILocalStorageService localStorage, ITokenRefreshService refresh)
    {
        _localStorage = localStorage;
        _refresh = refresh;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsStringAsync("auth_token");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var newToken = await _refresh.TryRefreshAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(newToken))
            return response;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
