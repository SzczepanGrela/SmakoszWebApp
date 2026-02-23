namespace Smakosz.Application.Features.Auth.Dtos;

public class AuthResultDto
{
    public string AccessToken { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
    public DateTime ExpiresAt { get; init; }
    public required UserProfileDto User { get; init; }
}

public class UserProfileDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = default!;
    public bool EmailVerified { get; init; }
}
