using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.DeleteAvatar;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.DeleteAvatar;

[Trait("Category", "Handlers")]
public class DeleteAvatarHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteAvatarHandler _handler;

    public DeleteAvatarHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new DeleteAvatarHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HasAvatar_EnqueuesR2KeysAndClearsUserFields()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).WithAvatarUrl("https://cdn.smakosz.test/uploads/user/abc.webp").Build());
        _sets.MediaAssets.Add(new Smakosz.Domain.Entities.MediaAsset
        {
            AssetId = 50,
            EntityType = MediaEntityType.User,
            EntityId = 1,
            Url = "https://cdn.smakosz.test/uploads/user/abc.webp"
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteAvatarCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single().AvatarUrl.Should().BeNull();
        _sets.Users.Single().AvatarBlurhash.Should().BeNull();
        _sets.MediaAssets.Should().BeEmpty();
        _sets.FilesToDelete.Should().ContainSingle(f => f.R2Key == "uploads/user/abc.webp" && f.Reason == "avatar_deleted");
    }

    [Fact]
    public async Task Handle_NoAvatar_ReturnsSuccessIdempotent()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteAvatarCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.FilesToDelete.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new DeleteAvatarHandler(_db, anonymous);

        var result = await handler.Handle(new DeleteAvatarCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
