using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.TwoFactor;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.TwoFactor;

[Trait("Category", "Handlers")]
public class Confirm2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodeHasher _codeHasher;
    private readonly Confirm2faHandler _handler;

    public Confirm2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _codeHasher = Substitute.For<ICodeHasher>();
        _handler = new Confirm2faHandler(_db, _currentUser, _codeHasher);
    }

    [Fact]
    public async Task Handle_ValidCode_Enables2fa()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithCode("hashed_code")
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.Is2faEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithCode("hashed_code")
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("wrong", "hashed_code").Returns(false);

        var result = await _handler.Handle(new Confirm2faCommand("wrong"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
        user.Is2faEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .AsExpired()
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_AlreadyEnabled_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_2FA_ALREADY_ENABLED");
    }

    [Fact]
    public async Task Handle_NoCodeExists_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_MaxAttemptsExceeded_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithCode("hashed_code")
            .Build();
        code.AttemptsCount = 10;
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_ValidCode_LogsSecurityEvent()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithCode("hashed_code")
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.TwoFactorEnabled);
    }

    [Fact]
    public async Task Handle_CustomMaxAttempts_RespectsConfig()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithCode("hashed_code")
            .Build();
        code.AttemptsCount = 4;
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        _sets.SystemConfigs.Add(new SystemConfig
        {
            Key = "auth.verify_code_max_attempts",
            Value = "5"
        });
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new Confirm2faCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
