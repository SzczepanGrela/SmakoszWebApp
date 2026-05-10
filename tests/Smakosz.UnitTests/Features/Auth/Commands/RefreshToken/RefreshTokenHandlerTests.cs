using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.RefreshToken;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.RefreshToken;

[Trait("Category", "Handlers")]
public class RefreshTokenHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISessionService _sessionService;
    private readonly IBusinessMetrics _metrics;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _sessionService = Substitute.For<ISessionService>();
        _metrics = Substitute.For<IBusinessMetrics>();

        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<TimeSpan>()).Returns("new_access_token");
        _sessionService.RotateSessionAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>())
            .Returns(new SessionTokenResult("new_refresh_token", DateTime.UtcNow.AddDays(7)));
        _sessionService.GetAccessTokenLifetimeSecondsAsync(Arg.Any<CancellationToken>()).Returns(900);

        _handler = new RefreshTokenHandler(_db, _jwtTokenService, _sessionService, _metrics);
    }

    [Fact]
    public async Task Handle_ValidToken_RotatesTokensAndReturnsAuthResult()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash("hashed_token")
            .Build();
        _sessionService.FindActiveSessionAsync("valid_refresh_token", Arg.Any<CancellationToken>()).Returns(session);
        var command = new RefreshTokenCommand("valid_refresh_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("new_access_token");
        result.Value.RefreshToken.Should().Be("new_refresh_token");
        await _sessionService.Received(1).RotateSessionAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsError()
    {
        _sessionService.FindActiveSessionAsync("nonexistent_token", Arg.Any<CancellationToken>()).Returns((UserSession?)null);
        var command = new RefreshTokenCommand("nonexistent_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsError()
    {
        _sessionService.FindActiveSessionAsync("expired_token", Arg.Any<CancellationToken>()).Returns((UserSession?)null);
        var command = new RefreshTokenCommand("expired_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_RevokedToken_ReturnsError()
    {
        _sessionService.FindActiveSessionAsync("revoked_token", Arg.Any<CancellationToken>()).Returns((UserSession?)null);
        var command = new RefreshTokenCommand("revoked_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsError()
    {
        var user = new UserBuilder().AsDeleted().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash("hashed_token")
            .Build();
        _sessionService.FindActiveSessionAsync("valid_token", Arg.Any<CancellationToken>()).Returns(session);
        var command = new RefreshTokenCommand("valid_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }
}
