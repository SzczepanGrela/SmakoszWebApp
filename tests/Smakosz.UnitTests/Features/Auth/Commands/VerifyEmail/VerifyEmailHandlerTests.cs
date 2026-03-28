using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.VerifyEmail;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.VerifyEmail;

[Trait("Category", "Handlers")]
public class VerifyEmailHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICodeHasher _codeHasher;
    private readonly VerifyEmailHandler _handler;

    public VerifyEmailHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _codeHasher = Substitute.For<ICodeHasher>();
        _handler = new VerifyEmailHandler(_db, _codeHasher);
    }

    [Fact]
    public async Task Handle_ValidCode_VerifiesEmail()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").AsEmailNotVerified().Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("hashed_code").Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);
        var command = new VerifyEmailCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").AsEmailNotVerified().Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("hashed_code").Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("wrong_code", "hashed_code").Returns(false);
        var command = new VerifyEmailCommand("test@example.com", "wrong_code");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").AsEmailNotVerified().Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("hashed_code").AsExpired().Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var command = new VerifyEmailCommand("nonexistent@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_AlreadyVerified_ReturnsSilentSuccess()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("test@example.com", "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
