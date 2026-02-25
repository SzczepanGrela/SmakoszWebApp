using Smakosz.Domain.Entities;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class UserSessionBuilder
{
    private readonly UserSession _session = new()
    {
        UserSessionId = 1,
        UserId = 1,
        RefreshTokenHash = "valid-refresh-token",
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        IsRevoked = false,
        CreatedAt = DateTime.UtcNow,
        User = null!
    };

    public UserSessionBuilder WithId(long id) { _session.UserSessionId = id; return this; }
    public UserSessionBuilder WithUserId(int userId) { _session.UserId = userId; return this; }
    public UserSessionBuilder WithUser(User user) { _session.User = user; _session.UserId = user.UserId; return this; }
    public UserSessionBuilder WithRefreshToken(string token) { _session.RefreshTokenHash = token; return this; }
    public UserSessionBuilder WithExpiresAt(DateTime expiresAt) { _session.ExpiresAt = expiresAt; return this; }
    public UserSessionBuilder AsRevoked() { _session.IsRevoked = true; return this; }
    public UserSessionBuilder AsExpired() { _session.ExpiresAt = DateTime.UtcNow.AddDays(-1); return this; }

    public UserSession Build() => _session;
}
