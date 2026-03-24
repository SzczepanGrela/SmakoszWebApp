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
    private readonly ICurrentUserService _currentUser;
    private readonly VerifyEmailHandler _handler;

    public VerifyEmailHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new VerifyEmailHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCode_VerifiesEmail()
    {
        var user = new UserBuilder().WithId(1).AsEmailNotVerified().Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("123456").Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new VerifyEmailHandler(_db, anonymousUser);
        var command = new VerifyEmailCommand("123456");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("123456").Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("wrong_code");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("123456").AsExpired().Build();
        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        // Arrange - verification code exists but user doesn't (edge case)
        var user = new UserBuilder().WithId(1).Build();
        var code = new VerificationCodeBuilder().WithUser(user).WithCode("123456").Build();
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new VerifyEmailCommand("123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
