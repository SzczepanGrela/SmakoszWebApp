using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Users.Queries.GetUserFollowing;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Users.Queries.GetUserFollowing;

[Trait("Category", "Handlers")]
public class GetUserFollowingHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetUserFollowingHandler _handler;

    public GetUserFollowingHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetUserFollowingHandler(_db);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsFollowingListForUser()
    {
        var source = new UserBuilder().WithId(1).WithSlug("source-user").Build();
        var followed1 = new UserBuilder().WithId(20).WithUsername("followed1").WithSlug("followed1").Build();
        var followed2 = new UserBuilder().WithId(21).WithUsername("followed2").WithSlug("followed2").Build();
        _sets.Users.Add(source);
        _sets.Users.Add(followed1);
        _sets.Users.Add(followed2);

        _sets.UserFollows.Add(new UserFollow
        {
            FollowerId = 1, FollowedId = 20, Follower = source, Followed = followed1, CreatedAt = DateTime.UtcNow
        });
        _sets.UserFollows.Add(new UserFollow
        {
            FollowerId = 1, FollowedId = 21, Follower = source, Followed = followed2, CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetUserFollowingQuery("source-user", new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new GetUserFollowingQuery("nonexistent-slug", new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
