using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.FollowUser;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.FollowUser;

[Trait("Category", "Handlers")]
public class FollowUserHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly FollowUserHandler _handler;

    public FollowUserHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new FollowUserHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidFollow_Succeeds()
    {
        var target = new UserBuilder().WithId(2).WithSlug("targetuser").Build();
        _sets.Users.Add(target);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new FollowUserCommand("targetuser"), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SelfFollow_ReturnsError()
    {
        var self = new UserBuilder().WithId(1).WithSlug("myself").Build();
        _sets.Users.Add(self);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new FollowUserCommand("myself"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FOLLOW_CANNOT_FOLLOW_SELF");
    }

    [Fact]
    public async Task Handle_AlreadyFollowing_ReturnsError()
    {
        var target = new UserBuilder().WithId(2).WithSlug("targetuser").Build();
        _sets.Users.Add(target);
        _sets.UserFollows.Add(new UserFollow { FollowerId = 1, FollowedId = 2, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new FollowUserCommand("targetuser"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FOLLOW_ALREADY_FOLLOWING");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new FollowUserCommand("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonUserRole_ReturnsUserRoleOnlyError()
    {
        var adminUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "Admin");
        var handler = new FollowUserHandler(_db, adminUser);

        var result = await handler.Handle(new FollowUserCommand("someone"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SOCIAL_USER_ROLE_ONLY");
    }
}
