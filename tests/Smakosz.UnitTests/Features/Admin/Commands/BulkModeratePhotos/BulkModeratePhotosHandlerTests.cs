using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.BulkModeratePhotos;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.BulkModeratePhotos;

[Trait("Category", "Handlers")]
public class BulkModeratePhotosHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublicConfigProvider _configProvider;
    private readonly BulkModeratePhotosHandler _handler;

    public BulkModeratePhotosHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _configProvider = Substitute.For<IPublicConfigProvider>();
        _configProvider.GetIntAsync("bulk_photo_moderation_max_count", 50, Arg.Any<CancellationToken>())
            .Returns(50);
        _handler = new BulkModeratePhotosHandler(_db, _currentUser, _configProvider);
    }

    private void SeedReason(string code = "photo_nudity")
    {
        _sets.RejectionReasons.Add(new RejectionReason
        {
            ReasonCode = code,
            Category = RejectionReasonCategory.Photo,
            AdminLabel = "Nagość",
            UserMessageTemplate = "Zdjęcie zawiera nagość.",
            IsActive = true
        });
    }

    private MediaAsset SeedPendingAsset(int assetId, int? uploaderId = null)
    {
        if (uploaderId.HasValue && _sets.Users.All(u => u.UserId != uploaderId.Value))
            _sets.Users.Add(new UserBuilder().WithId(uploaderId.Value).Build());

        var asset = new MediaAsset
        {
            AssetId = assetId,
            PublicId = Guid.NewGuid(),
            EntityType = MediaEntityType.Dish,
            EntityId = assetId,
            Url = $"http://img-{assetId}.jpg",
            ModerationStatus = ContentModerationStatus.Pending,
            UploadedBy = uploaderId
        };
        _sets.MediaAssets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task Handle_ApproveBatch_AllSucceed_PersistsAndReturnsSuccess()
    {
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        var a2 = SeedPendingAsset(2, uploaderId: 10);
        var a3 = SeedPendingAsset(3, uploaderId: 11);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId, a2.PublicId, a3.PublicId }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Success.Should().HaveCount(3);
        result.Value.Failed.Should().BeEmpty();
        _sets.MediaAssets.All(a => a.ModerationStatus == ContentModerationStatus.Approved).Should().BeTrue();
        _sets.ModerationLogs.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_RejectBatch_AllSucceed_CreatesNotificationsAndModerationLogs()
    {
        SeedReason();
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        var a2 = SeedPendingAsset(2, uploaderId: 11);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId, a2.PublicId }, false, new[] { "photo_nudity" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Success.Should().HaveCount(2);
        _sets.MediaAssets.All(a => a.ModerationStatus == ContentModerationStatus.Rejected).Should().BeTrue();
        _sets.Notifications.Should().HaveCount(2);
        _sets.ModerationLogs.Should().HaveCount(2);
        _sets.ModerationLogs[0].ReasonCodes.Should().BeEquivalentTo(new[] { "photo_nudity" });
    }

    [Fact]
    public async Task Handle_MixedValidAndMissing_ReturnsPartialSuccess()
    {
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        var a2 = SeedPendingAsset(2, uploaderId: 10);
        var ghostId = Guid.NewGuid();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId, ghostId, a2.PublicId }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Success.Should().HaveCount(2).And.Contain(new[] { a1.PublicId, a2.PublicId });
        result.Value.Failed.Should().ContainSingle();
        result.Value.Failed[0].PublicId.Should().Be(ghostId);
        result.Value.Failed[0].ErrorCode.Should().Be("PHOTO_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AlreadyModerated_ReportsFailure()
    {
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        var a2 = SeedPendingAsset(2, uploaderId: 10);
        a2.ModerationStatus = ContentModerationStatus.Approved;
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId, a2.PublicId }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Success.Should().ContainSingle().And.Contain(a1.PublicId);
        result.Value.Failed.Should().ContainSingle();
        result.Value.Failed[0].ErrorCode.Should().Be("PHOTO_ALREADY_MODERATED");
    }

    [Fact]
    public async Task Handle_OverLimit_ReturnsBulkLimitExceeded()
    {
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(ids, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_BULK_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsBulkEmpty()
    {
        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(Array.Empty<Guid>(), true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_BULK_EMPTY");
    }

    [Fact]
    public async Task Handle_RejectWithoutReason_ReturnsRejectionRequiredError()
    {
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId }, false, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task Handle_NonAdminAndNonModerator_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new BulkModeratePhotosHandler(_db, nonAdmin, _configProvider);

        var result = await handler.Handle(
            new BulkModeratePhotosCommand(new[] { Guid.NewGuid() }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ModeratorRole_Allowed()
    {
        var moderator = MockExtensions.CreateAuthenticatedUser(userId: 7, role: "Moderator");
        var handler = new BulkModeratePhotosHandler(_db, moderator, _configProvider);
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Success.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ApproveIncrementsUploaderPhotoCount()
    {
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        var a2 = SeedPendingAsset(2, uploaderId: 10);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId, a2.PublicId }, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Single(u => u.UserId == 10).PhotoCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_RejectResolvesPendingSystemTicket()
    {
        SeedReason();
        var a1 = SeedPendingAsset(1, uploaderId: 10);
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.Photo,
            ReferenceId = a1.AssetId,
            Status = TicketStatus.Open
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new BulkModeratePhotosCommand(new[] { a1.PublicId }, false, new[] { "photo_nudity" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Single().Status.Should().Be(TicketStatus.Resolved);
    }
}
