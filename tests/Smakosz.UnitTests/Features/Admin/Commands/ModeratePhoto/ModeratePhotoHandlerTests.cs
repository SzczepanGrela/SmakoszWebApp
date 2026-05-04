using FluentAssertions;
using NSubstitute;
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
    private readonly IReviewVisibilityRecalculator _visibility;
    private readonly IPrimaryPhotoSyncer _photoSyncer;
    private readonly ModeratePhotoHandler _handler;

    public ModeratePhotoHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _visibility = Substitute.For<IReviewVisibilityRecalculator>();
        _photoSyncer = Substitute.For<IPrimaryPhotoSyncer>();
        _handler = new ModeratePhotoHandler(_db, _currentUser, _visibility, _photoSyncer);
    }

    private void SeedReasons()
    {
        _sets.RejectionReasons.AddRange(new[]
        {
            new RejectionReason
            {
                ReasonCode = "photo_nudity",
                Category = RejectionReasonCategory.Photo,
                AdminLabel = "Nagość",
                UserMessageTemplate = "Zdjęcie zawiera nagość.",
                IsActive = true
            },
            new RejectionReason
            {
                ReasonCode = "photo_poor_quality",
                Category = RejectionReasonCategory.Photo,
                AdminLabel = "Niska jakość",
                UserMessageTemplate = "Zdjęcie jest rozmazane.",
                IsActive = true
            },
            new RejectionReason
            {
                ReasonCode = "text_spam",
                Category = RejectionReasonCategory.Text,
                AdminLabel = "Spam",
                UserMessageTemplate = "Spam tekstowy.",
                IsActive = true
            }
        });
    }

    private (User user, MediaAsset asset) SeedUserAndAsset()
    {
        var user = new UserBuilder().WithId(1).Build();
        var asset = new MediaAsset
        {
            AssetId = 1,
            PublicId = Guid.NewGuid(),
            EntityType = MediaEntityType.Dish,
            EntityId = 1,
            Url = "http://img.jpg",
            ModerationStatus = ContentModerationStatus.Pending,
            UploadedBy = 1
        };
        _sets.Users.Add(user);
        _sets.MediaAssets.Add(asset);
        return (user, asset);
    }

    [Fact]
    public async Task Handle_Approve_SetsApprovedStatusAndIncrementsPhotoCount()
    {
        var (user, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        user.PhotoCount.Should().Be(1);
        _sets.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Reject_WithSingleCode_ResolvesTemplateAndNotifies()
    {
        SeedReasons();
        var (_, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false, new[] { "photo_nudity" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
        asset.RejectionReason.Should().Be("Zdjęcie zawiera nagość.");
        _sets.Notifications.Should().ContainSingle();
        _sets.Notifications[0].Message.Should().Contain("Zdjęcie zawiera nagość.");
        _sets.ModerationLogs[0].ReasonCodes.Should().BeEquivalentTo(new[] { "photo_nudity" });
    }

    [Fact]
    public async Task Handle_Reject_WithMultipleCodesAndNote_ConcatenatesAll()
    {
        SeedReasons();
        var (_, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false,
                new[] { "photo_nudity", "photo_poor_quality" },
                "Również niewłaściwy kontekst."),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        asset.RejectionReason.Should().Be(
            "Zdjęcie zawiera nagość.\n\nZdjęcie jest rozmazane.\n\nDodatkowa uwaga moderatora: Również niewłaściwy kontekst.");
    }

    [Fact]
    public async Task Handle_Reject_WithTextCategoryCode_ReturnsCategoryMismatch()
    {
        SeedReasons();
        var (_, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false, new[] { "text_spam" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_CATEGORY_MISMATCH");
    }

    [Fact]
    public async Task Handle_Reject_WithNoCodeAndNoNote_ReturnsValidationError()
    {
        var (_, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task Handle_Reject_WithUnknownCode_ReturnsValidationError()
    {
        SeedReasons();
        var (_, asset) = SeedUserAndAsset();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModeratePhotoCommand(asset.PublicId, false, new[] { "photo_unknown" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_UNKNOWN_CODE");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ModeratePhotoHandler(_db, nonAdmin, _visibility, _photoSyncer);

        var result = await handler.Handle(
            new ModeratePhotoCommand(Guid.NewGuid(), true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_PhotoNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ModeratePhotoCommand(Guid.NewGuid(), true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PHOTO_NOT_FOUND");
    }
}
