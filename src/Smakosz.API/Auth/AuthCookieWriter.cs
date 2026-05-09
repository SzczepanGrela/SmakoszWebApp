namespace Smakosz.API.Auth;

// CSRF posture: SameSite=Strict on auth cookies blocks cross-site sub-requests including form POSTs.
// No third-party OAuth providers, so the strict policy does not break any redirect callback.
// Testing env relaxes to Lax so the Playwright suite can hit the API from the client origin.
public class AuthCookieWriter
{
    private readonly IHostEnvironment _env;

    public AuthCookieWriter(IHostEnvironment env)
    {
        _env = env;
    }

    public void Write(HttpResponse response, string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires)
    {
        response.Cookies.Append(CookieNames.Access, accessToken, BuildOptions(accessExpires));
        response.Cookies.Append(CookieNames.Refresh, refreshToken, BuildOptions(refreshExpires));
    }

    public void Clear(HttpResponse response)
    {
        var deletionOptions = BuildOptions(null);
        response.Cookies.Delete(CookieNames.Access, deletionOptions);
        response.Cookies.Delete(CookieNames.Refresh, deletionOptions);
    }

    private CookieOptions BuildOptions(DateTimeOffset? expires)
    {
        var isTesting = _env.IsEnvironment("Testing");
        return new CookieOptions
        {
            HttpOnly = true,
            // Secure only in Production: WebApplicationFactory serves HTTP so a Secure cookie would never
            // ride a test request, and the local dev server also runs HTTP unless dev-certs are wired.
            Secure = _env.IsProduction(),
            SameSite = isTesting ? SameSiteMode.Lax : SameSiteMode.Strict,
            Path = "/",
            Expires = expires,
            IsEssential = true,
        };
    }
}
