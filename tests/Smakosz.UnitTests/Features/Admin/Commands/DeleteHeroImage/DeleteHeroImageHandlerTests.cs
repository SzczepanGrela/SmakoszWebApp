using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteHeroImage;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteHeroImage;

[Trait("Category", "Handlers")]
public class DeleteHeroImageHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;

    public DeleteHeroImageHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
    }

    [Fact]
    public async Task Handle_ValidHero_EnqueuesR2AndRemovesRow()
    {
        var publicId = Guid.NewGuid();
        _sets.MediaAssets.Add(new Smakosz.Domain.Entities.MediaAsset
        {
            AssetId = 10,
            PublicId = publicId,
            EntityType = MediaEntityType.Hero,
            EntityId = 0,
            Url = "https://cdn.smakosz.test/uploads/hero/h1.webp"
        });
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = new DeleteHeroImageHandler(_db, MockExtensions.CreateAdminUser());

        var result = await handler.Handle(new DeleteHeroImageCommand(publicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MediaAssets.Should().BeEmpty();
        _sets.FilesToDelete.Should().ContainSingle(f => f.R2Key == "uploads/hero/h1.webp" && f.Reason == "hero_deleted");
    }

    [Fact]
    public async Task Handle_NotHeroEntityType_ReturnsValidationError()
    {
        var publicId = Guid.NewGuid();
        _sets.MediaAssets.Add(new Smakosz.Domain.Entities.MediaAsset
        {
            AssetId = 11,
            PublicId = publicId,
            EntityType = MediaEntityType.Dish,
            EntityId = 1,
            Url = "https://cdn.smakosz.test/uploads/dish/d1.webp"
        });
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = new DeleteHeroImageHandler(_db, MockExtensions.CreateAdminUser());

        var result = await handler.Handle(new DeleteHeroImageCommand(publicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_INVALID_FORMAT");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        var handler = new DeleteHeroImageHandler(_db, MockExtensions.CreateAdminUser());

        var result = await handler.Handle(new DeleteHeroImageCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PHOTO_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var regular = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new DeleteHeroImageHandler(_db, regular);

        var result = await handler.Handle(new DeleteHeroImageCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
