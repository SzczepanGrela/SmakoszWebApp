using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Media.Commands.DeleteMedia;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Media.Commands.DeleteMedia;

[Trait("Category", "Handlers")]
public class DeleteMediaHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly DeleteMediaHandler _handler;

    public DeleteMediaHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _storage = Substitute.For<IFileStorageService>();
        _handler = new DeleteMediaHandler(_db, _currentUser, _storage);
    }

    [Fact]
    public async Task Handle_OwnerDeletes_RemovesAsset()
    {
        var asset = new MediaAsset
        {
            AssetId = 1, PublicId = Guid.CreateVersion7(), EntityType = MediaEntityType.Dish,
            Url = "http://img.jpg", ModerationStatus = ContentModerationStatus.Approved, UploadedBy = 1
        };
        _sets.MediaAssets.Add(asset);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteMediaCommand(asset.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MediaAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AdminDeletes_RemovesAsset()
    {
        var adminUser = MockExtensions.CreateAdminUser();
        var handler = new DeleteMediaHandler(_db, adminUser, _storage);
        var asset = new MediaAsset
        {
            AssetId = 1, PublicId = Guid.CreateVersion7(), EntityType = MediaEntityType.Dish,
            Url = "http://img.jpg", ModerationStatus = ContentModerationStatus.Approved, UploadedBy = 999
        };
        _sets.MediaAssets.Add(asset);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new DeleteMediaCommand(asset.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MediaAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotOwnerOrAdmin_ReturnsError()
    {
        var asset = new MediaAsset
        {
            AssetId = 1, PublicId = Guid.CreateVersion7(), EntityType = MediaEntityType.Dish,
            Url = "http://img.jpg", ModerationStatus = ContentModerationStatus.Approved, UploadedBy = 999
        };
        _sets.MediaAssets.Add(asset);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteMediaCommand(asset.PublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new DeleteMediaCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PHOTO_NOT_FOUND");
    }
}
