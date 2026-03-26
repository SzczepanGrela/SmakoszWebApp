using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ModeratePhoto;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.ModeratePhoto;

[Trait("Category", "Handlers")]
public class ModeratePhotoHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ModeratePhotoHandler _handler;

    public ModeratePhotoHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new ModeratePhotoHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_Approve_SetsApprovedStatus()
    {
        var user = new UserBuilder().WithId(1).Build();
        var asset = new MediaAsset
        {
            AssetId = 1, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Dish,
            EntityId = 1, Url = "http://img.jpg", Status = MediaAssetStatus.Pending, UploadedBy = 1
        };
        _sets.Users.Add(user);
        _sets.MediaAssets.Add(asset);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, true, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.Status.Should().Be(MediaAssetStatus.Approved);
    }

    [Fact]
    public async Task Handle_Reject_SetsRejectedAndSendsNotification()
    {
        var user = new UserBuilder().WithId(1).Build();
        var asset = new MediaAsset
        {
            AssetId = 1, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Dish,
            EntityId = 1, Url = "http://img.jpg", Status = MediaAssetStatus.Pending, UploadedBy = 1
        };
        _sets.Users.Add(user);
        _sets.MediaAssets.Add(asset);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false, "Blurry"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.Status.Should().Be(MediaAssetStatus.Rejected);
        asset.RejectionReason.Should().Be("Blurry");
        _sets.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ModeratePhotoHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ModeratePhotoCommand(Guid.NewGuid(), true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_PhotoNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ModeratePhotoCommand(Guid.NewGuid(), true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PHOTO_NOT_FOUND");
    }
}
