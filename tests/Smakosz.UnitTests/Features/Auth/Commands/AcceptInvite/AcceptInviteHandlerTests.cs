using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.AcceptInvite;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.AcceptInvite;

[Trait("Category", "Handlers")]
public class AcceptInviteHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICodeHasher _codeHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly AcceptInviteHandler _handler;

    public AcceptInviteHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-pw");
        _codeHasher = Substitute.For<ICodeHasher>();
        _currentUser = MockExtensions.CreateAnonymousUser();
        _handler = new AcceptInviteHandler(_db, _passwordHasher, _codeHasher, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidInvite_SetsPasswordRemovesCodeAndLogs()
    {
        _sets.Users.Add(new UserBuilder().WithId(10).WithEmail("invitee@test.com").WithPasswordHash(string.Empty).Build());
        _sets.VerificationCodes.Add(new VerificationCode
        {
            UserId = 10,
            CodeHash = "hashed-code",
            Type = VerificationCodeType.Invitation,
            ExpiresAt = DateTime.UtcNow.AddHours(20)
        });
        _codeHasher.Verify("123456", "hashed-code").Returns(true);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AcceptInviteCommand("invitee@test.com", "123456", "NewSecure!1"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single(u => u.UserId == 10).PasswordHash.Should().Be("hashed-pw");
        _sets.VerificationCodes.Should().BeEmpty();
        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.PasswordChanged && l.UserId == 10);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCode()
    {
        var result = await _handler.Handle(
            new AcceptInviteCommand("ghost@test.com", "123456", "NewSecure!1"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsInvalidCode()
    {
        _sets.Users.Add(new UserBuilder().WithId(11).WithEmail("expired@test.com").Build());
        _sets.VerificationCodes.Add(new VerificationCode
        {
            UserId = 11,
            CodeHash = "hashed-code",
            Type = VerificationCodeType.Invitation,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AcceptInviteCommand("expired@test.com", "123456", "NewSecure!1"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_WrongCodeType_ReturnsInvalidCode()
    {
        _sets.Users.Add(new UserBuilder().WithId(12).WithEmail("wrongtype@test.com").Build());
        _sets.VerificationCodes.Add(new VerificationCode
        {
            UserId = 12,
            CodeHash = "hashed-code",
            Type = VerificationCodeType.ResetPassword,
            ExpiresAt = DateTime.UtcNow.AddHours(20)
        });
        _codeHasher.Verify("123456", "hashed-code").Returns(true);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AcceptInviteCommand("wrongtype@test.com", "123456", "NewSecure!1"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }
}
