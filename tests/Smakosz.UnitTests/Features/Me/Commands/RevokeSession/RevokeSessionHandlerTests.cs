using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.RevokeSession;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.RevokeSession;

[Trait("Category", "Handlers")]
public class RevokeSessionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly RevokeSessionHandler _handler;

    public RevokeSessionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, sessionId: 10);
        _handler = new RevokeSessionHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidSession_RevokesIt()
    {
        _sets.UserSessions.Add(new UserSession { UserSessionId = 20, UserId = 1, ExpiresAt = DateTime.UtcNow.AddDays(1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new RevokeSessionCommand(20), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CurrentSession_ReturnsError()
    {
        var result = await _handler.Handle(new RevokeSessionCommand(10), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SESSION_CANNOT_REVOKE_CURRENT");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new RevokeSessionCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SESSION_NOT_FOUND");
    }
}
