using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetPendingPhotos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetPendingPhotos;

[Trait("Category", "Handlers")]
public class GetPendingPhotosHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetPendingPhotosHandler _handler;

    public GetPendingPhotosHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetPendingPhotosHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPendingPhotos()
    {
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 1, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Dish,
            Url = "http://photo.jpg", ModerationStatus = ContentModerationStatus.Pending, CreatedAt = DateTime.UtcNow
        });
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 2, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Dish,
            Url = "http://approved.jpg", ModerationStatus = ContentModerationStatus.Approved, CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetPendingPhotosQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NeedsReviewAsset_AppearsInResults()
    {
        _sets.MediaAssets.Add(new MediaAsset
        {
            AssetId = 10, PublicId = Guid.NewGuid(), EntityType = MediaEntityType.Dish,
            Url = "http://uncertain.jpg", ModerationStatus = ContentModerationStatus.NeedsReview, CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetPendingPhotosQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetPendingPhotosHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetPendingPhotosQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
