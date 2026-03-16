using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Queries.GetMyProfile;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Queries.GetMyProfile;

[Trait("Category", "Handlers")]
public class GetMyProfileHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMyProfileHandler _handler;

    public GetMyProfileHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetMyProfileHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsProfileWithCorrectFields()
    {
        var user = new UserBuilder()
            .WithId(1)
            .WithUsername("testuser")
            .WithEmail("test@example.com")
            .WithSlug("testuser")
            .WithFollowersCount(10)
            .WithFollowingCount(7)
            .Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Username.Should().Be("testuser");
        result.Value.Email.Should().Be("test@example.com");
        result.Value.Slug.Should().Be("testuser");
        result.Value.FollowersCount.Should().Be(10);
        result.Value.FollowingCount.Should().Be(7);
    }

    [Fact]
    public async Task Handle_UserDoesNotExist_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
