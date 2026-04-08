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
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ITurnstileService _turnstile;
    private readonly IValidationConfigProvider _config;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _sessionService = Substitute.For<ISessionService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _turnstile = Substitute.For<ITurnstileService>();
        _config = Substitute.For<IValidationConfigProvider>();

        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _jwtTokenService.GenerateAccessToken(Arg.Any<Smakosz.Domain.Entities.User>(), Arg.Any<TimeSpan>()).Returns("access_token");
        _sessionService.CreateSessionAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns("refresh_token");
        _sessionService.GetAccessTokenLifetimeSecondsAsync(Arg.Any<CancellationToken>()).Returns(900);
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _turnstile.VerifyAsync(string.Empty, Arg.Any<CancellationToken>()).Returns(false);
        _config.GetInt("auth.max_login_attempts", 5).Returns(5);
        _config.GetInt("auth.lockout_duration_min", 15).Returns(15);

        _handler = new LoginHandler(_db, _passwordHasher, _jwtTokenService, _sessionService, _currentUser, _turnstile, _config);
    }

    [Fact]
    public async Task Handle_MissingTurnstileToken_ReturnsCaptchaFailed()
    {
        var command = new LoginCommand("user@example.com", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_InvalidTurnstileToken_ReturnsCaptchaFailed()
    {
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "invalid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResult()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithUsername("testuser").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        result.Value.RefreshToken.Should().Be("refresh_token");
        result.Value.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentials()
    {
        var command = new LoginCommand("nobody@example.com", "password", TurnstileToken: "valid-token");

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
        var command = new LoginCommand("user@example.com", "wrongpassword", TurnstileToken: "valid-token");

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
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

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
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_ACCOUNT_BANNED");
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsInvalidCredentials()
    {
        var user = new UserBuilder().WithEmail("user@example.com").AsDeleted().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_RememberMe_CreatesSessionWithRememberMe()
    {
        var user = new UserBuilder().WithEmail("user@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", RememberMe: true, TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _sessionService.Received(1).CreateSessionAsync(user.UserId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LockedAccount_ReturnsAccountLocked()
    {
        var user = new UserBuilder().WithEmail("user@example.com").AsLocked(DateTime.UtcNow.AddMinutes(10)).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Handle_ExpiredLockout_AllowsLogin()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithFailedLoginCount(5).AsLocked(DateTime.UtcNow.AddMinutes(-1)).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedCount()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithFailedLoginCount(1).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var command = new LoginCommand("user@example.com", "wrongpassword", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        user.FailedLoginCount.Should().Be(2);
        user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WrongPassword_LocksAfterMaxAttempts()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithFailedLoginCount(4).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var command = new LoginCommand("user@example.com", "wrongpassword", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        user.FailedLoginCount.Should().Be(5);
        user.LockedUntilUtc.Should().NotBeNull();
        user.LockedUntilUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ResetsFailedCount()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithFailedLoginCount(3).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LockedAccount_LogsSecurityEvent()
    {
        var user = new UserBuilder().WithEmail("user@example.com").AsLocked(DateTime.UtcNow.AddMinutes(10)).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _sets.SecurityLogs.Should().ContainSingle(l =>
            l.EventType == Smakosz.Domain.Enums.SecurityEventType.FailedLogin &&
            l.Details != null && l.Details.Contains("account_locked"));
    }
}
