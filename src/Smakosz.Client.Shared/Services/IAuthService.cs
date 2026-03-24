using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<object>> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync();
}
