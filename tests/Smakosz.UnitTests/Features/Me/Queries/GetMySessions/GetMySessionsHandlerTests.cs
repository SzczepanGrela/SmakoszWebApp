using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Queries.GetMySessions;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Queries.GetMySessions;

[Trait("Category", "Handlers")]
public class GetMySessionsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMySessionsHandler _handler;

    public GetMySessionsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetMySessionsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsActiveSessionsForCurrentUser()
    {
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 100,
            UserId = 1,
            RefreshTokenHash = "current",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 200,
            UserId = 1,
            RefreshTokenHash = "other",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        _sets.UserSessions.Add(new UserSession
        {
            UserSessionId = 300,
            UserId = 2,
            RefreshTokenHash = "other-user",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMySessionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(s => s.SessionId == 100 && s.IsCurrent);
        result.Value.Should().Contain(s => s.SessionId == 200 && !s.IsCurrent);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetMySessionsHandler(_db, anonymous);

        var result = await handler.Handle(new GetMySessionsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
