using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Smakosz.Client.Auth;

// Forces every outbound HttpClient request to ride the browser fetch credentials option so
// HttpOnly auth cookies travel with the request (otherwise Blazor WASM omits them by default).
public class BrowserCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
