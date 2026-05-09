using Smakosz.Client.Auth;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class AuthService : IAuthService
{
    private readonly SmakoszApiClient _api;
    private readonly CookieAuthStateProvider _authStateProvider;

    public AuthService(SmakoszApiClient api, CookieAuthStateProvider authStateProvider)
    {
        _api = api;
        _authStateProvider = authStateProvider;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/login", request);
        if (result is { Success: true })
            await _authStateProvider.NotifyUserAuthenticationAsync();
        return result;
    }

    public Task<ApiResponse<object>> RegisterAsync(RegisterRequest request)
        => _api.PostApiResponseAsync<object>("/api/auth/register", request);

    public Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request)
        => _api.PostApiResponseAsync<object>("/api/auth/verify-email", request);

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        // refreshToken parameter is ignored after cookie migration; the server reads the refresh cookie.
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/refresh", new { });
        if (result is { Success: true })
            await _authStateProvider.NotifyUserAuthenticationAsync();
        return result;
    }

    public async Task LogoutAsync()
    {
        await _api.PostApiResponseAsync<object>("/api/auth/logout", new { });
        _authStateProvider.NotifyUserLogout();
    }

    public Task<ApiResponse<object>> ForgotPasswordAsync(string email, string? turnstileToken = null)
        => _api.PostApiResponseAsync<object>("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email, TurnstileToken = turnstileToken });

    public Task<ApiResponse<object>> ResetPasswordAsync(string token, string newPassword, string confirmPassword)
        => _api.PostApiResponseAsync<object>("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = token,
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword
        });

    public async Task<ApiResponse<LoginResponse>> Verify2faAsync(string email, string code)
    {
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/verify-2fa", new Verify2faRequest { Email = email, Code = code });
        if (result is { Success: true })
            await _authStateProvider.NotifyUserAuthenticationAsync();
        return result;
    }

    public Task<ApiResponse<object>> Resend2faAsync(string email)
        => _api.PostApiResponseAsync<object>("/api/auth/resend-2fa", new Resend2faRequest { Email = email });

    public Task<ApiResponse<object>> ResendVerificationAsync(string email)
        => _api.PostApiResponseAsync<object>("/api/auth/resend-verification", new ResendVerificationRequest { Email = email });

    public Task<ApiResponse<object>> AcceptInviteAsync(string email, string code, string newPassword)
        => _api.PostApiResponseAsync<object>("/api/auth/accept-invite", new { Email = email, Code = code, NewPassword = newPassword });
}
