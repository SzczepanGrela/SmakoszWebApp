using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.ResetPassword;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.ResetPassword;

[Trait("Category", "Handlers")]
public class ResetPasswordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICodeHasher _codeHasher;
    private readonly ResetPasswordHandler _handler;

    public ResetPasswordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _codeHasher = Substitute.For<ICodeHasher>();
        _passwordHasher.Hash(Arg.Any<string>()).Returns("new_hashed_password");
        _handler = new ResetPasswordHandler(_db, _passwordHasher, _codeHasher);
    }

    [Fact]
    public async Task Handle_ValidCode_ChangesPassword()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_reset_code")
            .WithType(VerificationCodeType.ResetPassword)
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_reset_code").Returns(true);
        var command = new ResetPasswordCommand("test@example.com", "123456", "NewPassword123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.PasswordHash.Should().Be("new_hashed_password");
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_reset_code")
            .WithType(VerificationCodeType.ResetPassword)
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("wrong_code", "hashed_reset_code").Returns(false);
        var command = new ResetPasswordCommand("test@example.com", "wrong_code", "NewPassword123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        var code = new VerificationCodeBuilder()
            .WithUser(user)
            .WithCode("hashed_reset_code")
            .WithType(VerificationCodeType.ResetPassword)
            .AsExpired()
            .Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ResetPasswordCommand("test@example.com", "123456", "NewPassword123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var command = new ResetPasswordCommand("nonexistent@example.com", "123456", "NewPassword123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }
}
