using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.UnfollowUser;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.UnfollowUser;

[Trait("Category", "Handlers")]
public class UnfollowUserHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UnfollowUserHandler _handler;

    public UnfollowUserHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new UnfollowUserHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesFollowAndReturnsSuccess()
    {
        var target = new UserBuilder().WithId(2).WithSlug("target-user").Build();
        _sets.Users.Add(target);
        _sets.UserFollows.Add(new UserFollow
        {
            FollowerId = 1,
            FollowedId = 2,
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UnfollowUserCommand("target-user"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.UserFollows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotFollowing_ReturnsNotFollowingError()
    {
        var target = new UserBuilder().WithId(2).WithSlug("target-user").Build();
        _sets.Users.Add(target);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UnfollowUserCommand("target-user"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FOLLOW_NOT_FOLLOWING");
    }

    [Fact]
    public async Task Handle_NonUserRole_ReturnsUserRoleOnlyError()
    {
        var adminUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "Admin");
        var handler = new UnfollowUserHandler(_db, adminUser);

        var result = await handler.Handle(new UnfollowUserCommand("someone"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SOCIAL_USER_ROLE_ONLY");
    }
}
