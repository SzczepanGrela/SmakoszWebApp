using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.RefreshToken;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.RefreshToken;

[Trait("Category", "Handlers")]
public class RefreshTokenHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _jwtTokenService = Substitute.For<IJwtTokenService>();

        _jwtTokenService.GenerateAccessToken(Arg.Any<Smakosz.Domain.Entities.User>()).Returns("new_access_token");
        _jwtTokenService.GenerateRefreshToken().Returns("new_refresh_token");

        _handler = new RefreshTokenHandler(_db, _jwtTokenService);
    }

    [Fact]
    public async Task Handle_ValidToken_RotatesTokensAndReturnsAuthResult()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshToken("valid_refresh_token")
            .Build();
        _sets.Users.Add(user);
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RefreshTokenCommand("valid_refresh_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("new_access_token");
        result.Value.RefreshToken.Should().Be("new_refresh_token");
        session.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsError()
    {
        var command = new RefreshTokenCommand("nonexistent_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsError()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshToken("expired_token")
            .AsExpired()
            .Build();
        _sets.Users.Add(user);
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RefreshTokenCommand("expired_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_RevokedToken_ReturnsError()
    {
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshToken("revoked_token")
            .AsRevoked()
            .Build();
        _sets.Users.Add(user);
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
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
            .WithRefreshToken("valid_token")
            .Build();
        _sets.Users.Add(user);
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RefreshTokenCommand("valid_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_REFRESH_TOKEN");
    }
}
