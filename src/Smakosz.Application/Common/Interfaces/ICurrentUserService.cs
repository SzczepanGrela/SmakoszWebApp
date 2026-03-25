namespace Smakosz.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    long? SessionId { get; }
    string? Role { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

