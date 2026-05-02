using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ChangeUserRole;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.ChangeUserRole;

[Trait("Category", "Handlers")]
public class ChangeUserRoleHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ChangeUserRoleHandler _handler;

    public ChangeUserRoleHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99);
        _handler = new ChangeUserRoleHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_PromoteUserToModerator_UpdatesRoleAndWritesLogs()
    {
        var publicId = Guid.NewGuid();
        _sets.Users.Add(new UserBuilder().WithId(99).WithRole(UserRole.Admin).Build());
        _sets.Users.Add(new UserBuilder().WithId(5).WithPublicId(publicId).WithRole(UserRole.User).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeUserRoleCommand(publicId, UserRole.Moderator, "Promocja na moderatora"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single(u => u.UserId == 5).Role.Should().Be(UserRole.Moderator);
        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.RoleChanged && l.UserId == 5);
        _sets.AuditLogs.Should().ContainSingle(l => l.TableName == "users" && l.RecordId == 5 && l.Operation == AuditOperation.Update);
        _sets.Notifications.Should().ContainSingle(n => n.UserId == 5 && n.Type == NotificationType.System);
    }

    [Fact]
    public async Task Handle_PromoteUserToAdmin_Allowed()
    {
        var publicId = Guid.NewGuid();
        _sets.Users.Add(new UserBuilder().WithId(99).WithRole(UserRole.Admin).Build());
        _sets.Users.Add(new UserBuilder().WithId(6).WithPublicId(publicId).WithRole(UserRole.User).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeUserRoleCommand(publicId, UserRole.Admin, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single(u => u.UserId == 6).Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Handle_DemoteAdminWhenMultipleAdmins_Allowed()
    {
        var publicId = Guid.NewGuid();
        _sets.Users.Add(new UserBuilder().WithId(99).WithRole(UserRole.Admin).Build());
        _sets.Users.Add(new UserBuilder().WithId(7).WithPublicId(publicId).WithRole(UserRole.Admin).Build());
        _sets.Users.Add(new UserBuilder().WithId(8).WithRole(UserRole.Admin).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeUserRoleCommand(publicId, UserRole.User, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single(u => u.UserId == 7).Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task Handle_DemoteLastAdmin_ReturnsError()
    {
        var publicId = Guid.NewGuid();
        _sets.Users.Add(new UserBuilder().WithId(7).WithPublicId(publicId).WithRole(UserRole.Admin).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeUserRoleCommand(publicId, UserRole.User, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_CANNOT_DEMOTE_LAST_ADMIN");
    }

    [Fact]
    public async Task Handle_SelfRoleChange_ReturnsError()
    {
        var publicId = Guid.NewGuid();
        _sets.Users.Add(new UserBuilder().WithId(99).WithPublicId(publicId).WithRole(UserRole.Admin).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeUserRoleCommand(publicId, UserRole.User, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_CANNOT_CHANGE_OWN_ROLE");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Moderator");
        var handler = new ChangeUserRoleHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ChangeUserRoleCommand(Guid.NewGuid(), UserRole.Admin, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
