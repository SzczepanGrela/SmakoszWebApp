using Smakosz.Domain.Entities;

namespace Smakosz.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, TimeSpan lifetime);
    string GenerateRefreshToken();
}
