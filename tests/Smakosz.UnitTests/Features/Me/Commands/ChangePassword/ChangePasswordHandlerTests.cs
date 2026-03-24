using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.ChangePassword;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.ChangePassword;

[Trait("Category", "Handlers")]
public class ChangePasswordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ChangePasswordHandler _handler;

    public ChangePasswordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _passwordHasher.Hash(Arg.Any<string>()).Returns("new_hash");
        _handler = new ChangePasswordHandler(_db, _currentUser, _passwordHasher);
    }

    [Fact]
    public async Task Handle_ValidCurrentPassword_ChangesPassword()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("old_hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("OldPassword123", "old_hash").Returns(true);

        var result = await _handler.Handle(
            new ChangePasswordCommand("OldPassword123", "NewPassword456!"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.PasswordHash.Should().Be("new_hash");
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("old_hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("WrongPassword", "old_hash").Returns(false);

        var result = await _handler.Handle(
            new ChangePasswordCommand("WrongPassword", "NewPassword456!"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
