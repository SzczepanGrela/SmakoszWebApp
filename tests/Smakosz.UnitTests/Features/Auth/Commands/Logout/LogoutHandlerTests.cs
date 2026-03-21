using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Logout;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Logout;

[Trait("Category", "Handlers")]
public class LogoutHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ISessionService _sessionService;
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _sessionService = Substitute.For<ISessionService>();
        _handler = new LogoutHandler(_db, _sessionService);
    }

    [Fact]
    public async Task Handle_ValidSession_RevokesSession()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash("hashed_token")
            .Build();
        _sessionService.FindSessionForLogoutAsync("active_token", Arg.Any<CancellationToken>()).Returns(session);
        var command = new LogoutCommand("active_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sessionService.Received(1).RevokeSession(session);
    }

    [Fact]
    public async Task Handle_NoSession_ReturnsDeletedGracefully()
    {
        _sessionService.FindSessionForLogoutAsync("nonexistent_token", Arg.Any<CancellationToken>()).Returns((UserSession?)null);
        var command = new LogoutCommand("nonexistent_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sessionService.DidNotReceive().RevokeSession(Arg.Any<UserSession>());
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_ReturnsDeletedGracefully()
    {
        _sessionService.FindSessionForLogoutAsync("revoked_token", Arg.Any<CancellationToken>()).Returns((UserSession?)null);
        var command = new LogoutCommand("revoked_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
