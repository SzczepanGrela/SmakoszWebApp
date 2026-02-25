using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.RevokeAllSessions;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.RevokeAllSessions;

[Trait("Category", "Handlers")]
public class RevokeAllSessionsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly RevokeAllSessionsHandler _handler;

    public RevokeAllSessionsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new RevokeAllSessionsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RevokesAllSessionsExceptCurrent()
    {
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 100,
            UserId = 1,
            RefreshTokenHash = "current",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 200,
            UserId = 1,
            RefreshTokenHash = "other1",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 300,
            UserId = 1,
            RefreshTokenHash = "other2",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RevokeAllSessionsCommand(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.UserSessions.Should().HaveCount(1);
        _sets.UserSessions[0].UserSessionId.Should().Be(100);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new RevokeAllSessionsHandler(_db, anonymous);

        var result = await handler.Handle(
            new RevokeAllSessionsCommand(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
