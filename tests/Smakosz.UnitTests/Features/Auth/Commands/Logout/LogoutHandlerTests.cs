using FluentAssertions;
using Smakosz.Application.Features.Auth.Commands.Logout;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Logout;

[Trait("Category", "Handlers")]
public class LogoutHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new LogoutHandler(_db);
    }

    [Fact]
    public async Task Handle_ValidSession_RevokesSession()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshToken("active_token")
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LogoutCommand("active_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        session.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoSession_ReturnsDeletedGracefully()
    {
        var command = new LogoutCommand("nonexistent_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_ReturnsDeletedGracefully()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshToken("revoked_token")
            .AsRevoked()
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LogoutCommand("revoked_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - gracefully returns Deleted even though session was already revoked
        result.IsError.Should().BeFalse();
    }
}
