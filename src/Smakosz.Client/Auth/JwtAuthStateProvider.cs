using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Smakosz.Client.Auth;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public JwtAuthStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsStringAsync("auth_token");
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(_anonymous);

        var identity = ParseClaimsFromJwt(token);
        if (identity == null)
            return new AuthenticationState(_anonymous);

        var expClaim = identity.FindFirst("exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
        {
            var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
            if (expDate <= DateTimeOffset.UtcNow)
            {
                await _localStorage.RemoveItemAsync("auth_token");
                return new AuthenticationState(_anonymous);
            }
        }

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserAuthentication(string token)
    {
        var identity = ParseClaimsFromJwt(token);
        var user = identity != null ? new ClaimsPrincipal(identity) : _anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    private static ClaimsIdentity? ParseClaimsFromJwt(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var claims = new List<Claim>();
            foreach (var claim in jwt.Claims)
            {
                var type = claim.Type switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "name" or "unique_name" => ClaimTypes.Name,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    _ => claim.Type
                };
                claims.Add(new Claim(type, claim.Value));
            }

            return new ClaimsIdentity(claims, "jwt");
        }
        catch
        {
            return null;
        }
    }
}
