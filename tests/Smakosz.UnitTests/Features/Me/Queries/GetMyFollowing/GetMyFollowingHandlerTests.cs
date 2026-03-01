using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Queries.GetMyFollowing;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Queries.GetMyFollowing;

[Trait("Category", "Handlers")]
public class GetMyFollowingHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMyFollowingHandler _handler;

    public GetMyFollowingHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetMyFollowingHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsFollowing()
    {
        var followed = new UserBuilder().WithId(2).WithUsername("followed1").Build();
        _sets.UserFollows.Add(new UserFollow { FollowerId = 1, FollowedId = 2, Followed = followed, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetMyFollowingQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetMyFollowingHandler(_db, anonymous);

        var result = await handler.Handle(
            new GetMyFollowingQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
