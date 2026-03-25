using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Login;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Login;

[Trait("Category", "Handlers")]
public class LoginHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _currentUser = Substitute.For<ICurrentUserService>();

        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _jwtTokenService.GenerateAccessToken(Arg.Any<Smakosz.Domain.Entities.User>()).Returns("access_token");
        _jwtTokenService.GenerateRefreshToken().Returns("refresh_token");

        _handler = new LoginHandler(_db, _passwordHasher, _jwtTokenService, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResult()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithUsername("testuser").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        result.Value.RefreshToken.Should().Be("refresh_token");
        result.Value.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentials()
    {
        var command = new LoginCommand("nobody@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = new UserBuilder().WithEmail("user@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var command = new LoginCommand("user@example.com", "wrongpassword");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsAccountInactive()
    {
        var user = new UserBuilder().WithEmail("user@example.com").AsInactive().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_BannedUser_ReturnsAccountBanned()
    {
        var user = new UserBuilder().WithEmail("user@example.com").AsBanned().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_ACCOUNT_BANNED");
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsInvalidCredentials()
    {
        // Arrange - deleted users are excluded by the query (u.IsDeleted == false)
        var user = new UserBuilder().WithEmail("user@example.com").AsDeleted().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_RememberMe_SessionExpires30Days()
    {
        var user = new UserBuilder().WithEmail("user@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", RememberMe: true);

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        // The session was added to the backing list
        _sets.UserSessions.Should().ContainSingle();
        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromSeconds(5));
    }
}
