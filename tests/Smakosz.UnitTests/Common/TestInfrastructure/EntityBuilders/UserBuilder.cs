using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class UserBuilder
{
    private readonly User _user = new()
    {
        UserId = 1,
        PublicId = Guid.NewGuid(),
        Username = "testuser",
        Email = "test@example.com",
        PasswordHash = "hashed_password",
        Role = UserRole.User,
        IsActive = true,
        IsBanned = false,
        IsDeleted = false,
        EmailVerified = true,
        Slug = "testuser",
        SecurityStamp = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow
    };

    public UserBuilder WithId(int id) { _user.UserId = id; return this; }
    public UserBuilder WithPublicId(Guid id) { _user.PublicId = id; return this; }
    public UserBuilder WithEmail(string email) { _user.Email = email; return this; }
    public UserBuilder WithUsername(string username) { _user.Username = username; _user.Slug = username.ToLowerInvariant(); return this; }
    public UserBuilder WithPasswordHash(string hash) { _user.PasswordHash = hash; return this; }
    public UserBuilder WithSlug(string slug) { _user.Slug = slug; return this; }
    public UserBuilder WithRole(UserRole role) { _user.Role = role; return this; }
    public UserBuilder WithReviewCount(int count) { _user.ReviewCount = count; return this; }
    public UserBuilder WithFollowersCount(int count) { _user.FollowersCount = count; return this; }
    public UserBuilder WithFollowingCount(int count) { _user.FollowingCount = count; return this; }
    public UserBuilder WithAvatarUrl(string url) { _user.AvatarUrl = url; return this; }
    public UserBuilder AsInactive() { _user.IsActive = false; return this; }
    public UserBuilder AsBanned() { _user.IsBanned = true; return this; }
    public UserBuilder AsDeleted() { _user.IsDeleted = true; _user.DeletedAt = DateTime.UtcNow; return this; }
    public UserBuilder AsEmailNotVerified() { _user.EmailVerified = false; return this; }
    public UserBuilder With2faEnabled() { _user.Is2faEnabled = true; return this; }
    public UserBuilder WithFailedLoginCount(int count) { _user.FailedLoginCount = count; return this; }
    public UserBuilder AsLocked(DateTime until) { _user.LockedUntilUtc = until; return this; }

    public User Build() => _user;
}
