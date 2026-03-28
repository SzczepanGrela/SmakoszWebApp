using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<object>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync();
    Task<ApiResponse<object>> ForgotPasswordAsync(string email);
    Task<ApiResponse<object>> ResetPasswordAsync(string token, string newPassword, string confirmPassword);
    Task<ApiResponse<LoginResponse>> Verify2faAsync(string code);
    Task<ApiResponse<object>> Resend2faAsync();
    Task<ApiResponse<object>> ResendVerificationAsync(string email);
}
