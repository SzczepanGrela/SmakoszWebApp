using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.TwoFactor;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.TwoFactor;

[Trait("Category", "Handlers")]
public class Disable2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly Disable2faHandler _handler;

    public Disable2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _handler = new Disable2faHandler(_db, _currentUser, _passwordHasher, Substitute.For<ISecurityNotificationService>());
    }

    [Fact]
    public async Task Handle_ValidPassword_Disables2fa()
    {
        var user = new UserBuilder().WithId(1).With2faEnabled().WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("correct_password", "hash").Returns(true);

        var result = await _handler.Handle(new Disable2faCommand("correct_password"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.Is2faEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).With2faEnabled().WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("wrong", "hash").Returns(false);

        var result = await _handler.Handle(new Disable2faCommand("wrong"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        user.Is2faEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotEnabled_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("correct_password", "hash").Returns(true);

        var result = await _handler.Handle(new Disable2faCommand("correct_password"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_2FA_NOT_ENABLED");
    }

    [Fact]
    public async Task Handle_ValidPassword_LogsSecurityEvent()
    {
        var user = new UserBuilder().WithId(1).With2faEnabled().WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("correct_password", "hash").Returns(true);

        await _handler.Handle(new Disable2faCommand("correct_password"), CancellationToken.None);

        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.TwoFactorDisabled);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new Disable2faCommand("password"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
