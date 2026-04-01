using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Services;

public class SystemCurrentUserService : ICurrentUserService
{
    public int? UserId => null;
    public long? SessionId => null;
    public string? Role => "System";
    public bool IsAdmin => true;
    public bool IsAdminOrModerator => true;
    public bool IsAuthenticated => false;
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "Orchestrator";
}
