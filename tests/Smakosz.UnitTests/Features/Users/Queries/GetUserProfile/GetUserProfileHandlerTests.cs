using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Users.Queries.GetUserProfile;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Users.Queries.GetUserProfile;

[Trait("Category", "Handlers")]
public class GetUserProfileHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetUserProfileHandler _handler;

    public GetUserProfileHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetUserProfileHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ExistingUser_ReturnsProfile()
    {
        var user = new UserBuilder().WithId(1).WithSlug("testuser").WithUsername("TestUser").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetUserProfileQuery("testuser"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Slug.Should().Be("testuser");
        result.Value.Username.Should().Be("TestUser");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new GetUserProfileQuery("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithSlug("deleted").AsDeleted().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetUserProfileQuery("deleted"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
