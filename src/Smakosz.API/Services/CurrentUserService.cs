using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User
        .FindFirstValue(ClaimTypes.Role);

    public bool IsAdmin => Role == nameof(Domain.Enums.UserRole.Admin);

    public long? SessionId
    {
        get
        {
            var sid = _httpContextAccessor.HttpContext?.User
                .FindFirstValue("session_id");
            return long.TryParse(sid, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User
        .Identity?.IsAuthenticated ?? false;
}
