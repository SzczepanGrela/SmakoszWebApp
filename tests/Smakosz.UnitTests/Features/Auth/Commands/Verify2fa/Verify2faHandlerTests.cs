using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Verify2fa;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Verify2fa;

[Trait("Category", "Handlers")]
public class Verify2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICodeHasher _codeHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISessionService _sessionService;
    private readonly Verify2faHandler _handler;

    public Verify2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _codeHasher = Substitute.For<ICodeHasher>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _sessionService = Substitute.For<ISessionService>();
        _jwtTokenService.GenerateAccessToken(Arg.Any<Domain.Entities.User>(), Arg.Any<TimeSpan>()).Returns("access_token");
        _sessionService.CreateSessionAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SessionTokenResult("refresh_token", DateTime.UtcNow.AddDays(7)));
        _sessionService.GetAccessTokenLifetimeSecondsAsync(Arg.Any<CancellationToken>()).Returns(900);
        _handler = new Verify2faHandler(_db, _codeHasher, _jwtTokenService, _sessionService);
    }

    [Fact]
    public async Task Handle_ValidCode_ReturnsAuthResult()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_2fa")
            .WithType(VerificationCodeType.TwoFactorAuth)
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_2fa").Returns(true);
        var command = new Verify2faCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        result.Value.RefreshToken.Should().Be("refresh_token");
        await _sessionService.Received(1).CreateSessionAsync(user.UserId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PayloadRememberMe_CreatesSessionWithRememberMeTrue()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_2fa")
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithPayload("r")
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_2fa").Returns(true);
        var command = new Verify2faCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _sessionService.Received(1).CreateSessionAsync(user.UserId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PayloadNull_CreatesSessionWithRememberMeFalse()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_2fa")
            .WithType(VerificationCodeType.TwoFactorAuth)
            .WithPayload(null)
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_2fa").Returns(true);
        var command = new Verify2faCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _sessionService.Received(1).CreateSessionAsync(user.UserId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_2fa")
            .WithType(VerificationCodeType.TwoFactorAuth)
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("wrong_code", "hashed_2fa").Returns(false);
        var command = new Verify2faCommand("test@example.com", "wrong_code");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var command = new Verify2faCommand("nonexistent@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_2fa")
            .WithType(VerificationCodeType.TwoFactorAuth)
            .AsExpired()
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new Verify2faCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }
}
