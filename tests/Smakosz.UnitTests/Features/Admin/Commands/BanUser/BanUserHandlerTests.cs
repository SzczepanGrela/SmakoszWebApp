using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.BanUser;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.BanUser;

[Trait("Category", "Handlers")]
public class BanUserHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly BanUserHandler _handler;
    private static readonly Guid TestPublicId = Guid.NewGuid();

    public BanUserHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 99, role: "Admin");
        _handler = new BanUserHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidUser_BansThem()
    {
        var user = new UserBuilder().WithId(5).WithPublicId(TestPublicId).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new BanUserCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.IsBanned.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new BanUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new BanUserHandler(_db, nonAdmin);

        var result = await handler.Handle(new BanUserCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_AdminBanningSelf_ReturnsError()
    {
        var selfPublicId = Guid.NewGuid();
        var self = new UserBuilder().WithId(99).WithPublicId(selfPublicId).Build();
        _sets.Users.Add(self);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new BanUserCommand(selfPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_CANNOT_BAN_SELF");
        self.IsBanned.Should().BeFalse();
    }
}
