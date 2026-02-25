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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly Verify2faHandler _handler;

    public Verify2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _jwtTokenService.GenerateAccessToken(Arg.Any<Domain.Entities.User>()).Returns("access_token");
        _jwtTokenService.GenerateRefreshToken().Returns("refresh_token");
        _handler = new Verify2faHandler(_db, _passwordHasher, _jwtTokenService);
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
        _passwordHasher.Verify("123456", "hashed_2fa").Returns(true);
        var command = new Verify2faCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        result.Value.RefreshToken.Should().Be("refresh_token");
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
        _passwordHasher.Verify("wrong_code", "hashed_2fa").Returns(false);
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
