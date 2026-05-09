using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Login;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
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
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;
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
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _emailService = Substitute.For<IEmailService>();

        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _jwtTokenService.GenerateAccessToken(Arg.Any<Smakosz.Domain.Entities.User>(), Arg.Any<TimeSpan>()).Returns("access_token");
        _sessionService.CreateSessionAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SessionTokenResult("refresh_token", DateTime.UtcNow.AddDays(7)));
        _sessionService.GetAccessTokenLifetimeSecondsAsync(Arg.Any<CancellationToken>()).Returns(900);
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _turnstile.VerifyAsync(string.Empty, Arg.Any<CancellationToken>()).Returns(false);
        _config.GetInt("auth.max_login_attempts", 5).Returns(5);
        _config.GetInt("auth.lockout_duration_min", 15).Returns(15);
        _config.GetInt("auth.priv_ip_ban_threshold", 10).Returns(10);
        _config.GetInt("auth.priv_ip_ban_window_min", 15).Returns(15);
        _config.GetInt("auth.priv_ip_ban_hours", 1).Returns(1);

        _handler = new LoginHandler(_db, _passwordHasher, _jwtTokenService, _sessionService, _currentUser, _turnstile, _config, _verificationCodeService, _emailService, Substitute.For<IBusinessMetrics>(), Substitute.For<ISecurityNotificationService>());
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

    [Fact]
    public async Task Handle_UserWith2faEnabled_ReturnsTwoFactorRequired()
    {
        var user = new UserBuilder().WithEmail("user@example.com").With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _verificationCodeService
            .CreateCodeAsync(Arg.Any<int>(), Smakosz.Domain.Enums.VerificationCodeType.TwoFactorAuth, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("123456");
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_2FA_REQUIRED");
        await _emailService.Received(1).Send2faCodeAsync(user.Email, "123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserWithout2fa_ReturnsTokenDirectly()
    {
        var user = new UserBuilder().WithEmail("user@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new LoginCommand("user@example.com", "password", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        await _emailService.DidNotReceive().Send2faCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PrivilegedUser_DoesNotLockoutAfterMaxAttempts()
    {
        var user = new UserBuilder().WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        SeedFailedLoginLogs(9, "admin@example.com", "1.2.3.4");
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns("1.2.3.4");
        var command = new LoginCommand("admin@example.com", "wrongpassword", TurnstileToken: "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        user.LockedUntilUtc.Should().BeNull();
        user.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PrivilegedUser_BansIpAfterThresholdAttempts()
    {
        var user = new UserBuilder().WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        SeedFailedLoginLogs(9, "admin@example.com", "1.2.3.4");
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns("1.2.3.4");
        var command = new LoginCommand("admin@example.com", "wrongpassword", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _sets.BannedIdentifiers.Should().ContainSingle(b =>
            b.Type == BannedIdentifierType.Ip
            && b.Value == "1.2.3.4"
            && b.ExpiresAt.HasValue
            && b.ExpiresAt.Value > DateTime.UtcNow);
        _sets.SecurityLogs.Should().Contain(s => s.EventType == SecurityEventType.BlockedIp);
    }

    [Fact]
    public async Task Handle_PrivilegedUser_DoesNotBanIpBelowThreshold()
    {
        var user = new UserBuilder().WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        SeedFailedLoginLogs(8, "admin@example.com", "1.2.3.4");
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns("1.2.3.4");
        var command = new LoginCommand("admin@example.com", "wrongpassword", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _sets.BannedIdentifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PrivilegedUser_IdempotentBan()
    {
        var user = new UserBuilder().WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        SeedFailedLoginLogs(9, "admin@example.com", "1.2.3.4");
        _sets.BannedIdentifiers.Add(new BannedIdentifier
        {
            Type = BannedIdentifierType.Ip,
            Value = "1.2.3.4",
            BannedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            Reason = "Pre-existing"
        });
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns("1.2.3.4");
        var command = new LoginCommand("admin@example.com", "wrongpassword", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _sets.BannedIdentifiers
            .Count(b => b.Value == "1.2.3.4" && b.ExpiresAt.HasValue && b.ExpiresAt.Value > DateTime.UtcNow)
            .Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonPrivilegedUser_StillLocksOutNormally()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithRole(UserRole.User).WithFailedLoginCount(4).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns("1.2.3.4");
        var command = new LoginCommand("user@example.com", "wrongpassword", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        user.FailedLoginCount.Should().Be(5);
        user.LockedUntilUtc.Should().NotBeNull();
        user.LockedUntilUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
        _sets.BannedIdentifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PrivilegedUser_NullIp_DoesNotBan()
    {
        var user = new UserBuilder().WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        SeedFailedLoginLogs(9, "admin@example.com", ipAddress: null);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _currentUser.IpAddress.Returns((string?)null);
        var command = new LoginCommand("admin@example.com", "wrongpassword", TurnstileToken: "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _sets.BannedIdentifiers.Should().BeEmpty();
    }

    private void SeedFailedLoginLogs(int count, string email, string? ipAddress)
    {
        var baseTime = DateTime.UtcNow.AddMinutes(-5);
        for (int i = 0; i < count; i++)
        {
            _sets.SecurityLogs.Add(new SecurityLog
            {
                EventType = SecurityEventType.FailedLogin,
                IpAddress = ipAddress,
                Email = email.ToLowerInvariant(),
                CreatedAt = baseTime.AddSeconds(i)
            });
        }
    }
}
