using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Users.Queries.GetUserFollowers;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Users.Queries.GetUserFollowers;

[Trait("Category", "Handlers")]
public class GetUserFollowersHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetUserFollowersHandler _handler;

    public GetUserFollowersHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetUserFollowersHandler(_db);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsFollowersForUser()
    {
        var target = new UserBuilder().WithId(2).WithSlug("target-user").Build();
        var follower1 = new UserBuilder().WithId(10).WithUsername("follower1").WithSlug("follower1").Build();
        var follower2 = new UserBuilder().WithId(11).WithUsername("follower2").WithSlug("follower2").Build();
        _sets.Users.Add(target);
        _sets.Users.Add(follower1);
        _sets.Users.Add(follower2);

        _sets.UserFollows.Add(new UserFollow
        {
            FollowerId = 10, FollowedId = 2, Follower = follower1, Followed = target, CreatedAt = DateTime.UtcNow
        });
        _sets.UserFollows.Add(new UserFollow
        {
            FollowerId = 11, FollowedId = 2, Follower = follower2, Followed = target, CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetUserFollowersQuery("target-user", new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new GetUserFollowersQuery("nonexistent-slug", new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
