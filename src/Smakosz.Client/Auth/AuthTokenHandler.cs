using System.Net;

namespace Smakosz.Client.Auth;

// After cookie-based auth migration this handler no longer injects Bearer headers. Its sole job is to
// retry a request once when the response is 401 and the refresh service successfully rotates the cookie.
// HttpRequestMessage cannot be reused after Send so we have to clone it before the retry.
public class AuthTokenHandler : DelegatingHandler
{
    private readonly ITokenRefreshService _refresh;

    public AuthTokenHandler(ITokenRefreshService refresh)
    {
        _refresh = refresh;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var refreshed = await _refresh.TryRefreshAsync(cancellationToken);
        if (!refreshed)
            return response;

        response.Dispose();
        var retry = await CloneAsync(request);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri);
        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (source.Content is not null)
        {
            var ms = new MemoryStream();
            await source.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var header in source.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
