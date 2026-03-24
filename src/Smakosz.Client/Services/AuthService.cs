using Blazored.LocalStorage;
using Smakosz.Client.Auth;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class AuthService : IAuthService
{
    private readonly SmakoszApiClient _api;
    private readonly ILocalStorageService _localStorage;
    private readonly JwtAuthStateProvider _authStateProvider;

    public AuthService(SmakoszApiClient api, ILocalStorageService localStorage, JwtAuthStateProvider authStateProvider)
    {
        _api = api;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/login", request);
        if (result is { Success: true, Data: not null })
        {
            await _localStorage.SetItemAsStringAsync("auth_token", result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync("refresh_token", result.Data.RefreshToken);
            _authStateProvider.NotifyUserAuthentication(result.Data.AccessToken);
        }
        return result;
    }

    public async Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request)
    {
        var result = await _api.PostApiResponseAsync<RegisterResponse>("/api/auth/register", request);
        if (result is { Success: true, Data: not null })
        {
            await _localStorage.SetItemAsStringAsync("auth_token", result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync("refresh_token", result.Data.RefreshToken);
            _authStateProvider.NotifyUserAuthentication(result.Data.AccessToken);
        }
        return result;
    }

    public Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request)
        => _api.PostApiResponseAsync<object>("/api/auth/verify-email", request);

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken });
        if (result is { Success: true, Data: not null })
        {
            await _localStorage.SetItemAsStringAsync("auth_token", result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync("refresh_token", result.Data.RefreshToken);
            _authStateProvider.NotifyUserAuthentication(result.Data.AccessToken);
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _localStorage.GetItemAsStringAsync("refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _api.PostApiResponseAsync<object>("/api/auth/logout", new { RefreshToken = refreshToken });
        }
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("refresh_token");
        _authStateProvider.NotifyUserLogout();
    }

    public Task<ApiResponse<object>> ForgotPasswordAsync(string email)
        => _api.PostApiResponseAsync<object>("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });

    public Task<ApiResponse<object>> ResetPasswordAsync(string token, string newPassword, string confirmPassword)
        => _api.PostApiResponseAsync<object>("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = token,
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword
        });

    public async Task<ApiResponse<LoginResponse>> Verify2faAsync(string code)
    {
        var result = await _api.PostApiResponseAsync<LoginResponse>("/api/auth/verify-2fa", new Verify2faRequest { Code = code });
        if (result is { Success: true, Data: not null })
        {
            await _localStorage.SetItemAsStringAsync("auth_token", result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync("refresh_token", result.Data.RefreshToken);
            _authStateProvider.NotifyUserAuthentication(result.Data.AccessToken);
        }
        return result;
    }

    public Task<ApiResponse<object>> Resend2faAsync()
        => _api.PostApiResponseAsync<object>("/api/auth/resend-2fa", null);

    public Task<ApiResponse<object>> ResendVerificationAsync(string email)
        => _api.PostApiResponseAsync<object>("/api/auth/resend-verification", new ResendVerificationRequest { Email = email });
}
