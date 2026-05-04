using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ModerateReview;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.ModerateReview;

[Trait("Category", "Handlers")]
public class ModerateReviewHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IReviewVisibilityRecalculator _visibility;
    private readonly ModerateReviewHandler _handler;

    public ModerateReviewHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _visibility = Substitute.For<IReviewVisibilityRecalculator>();
        _handler = new ModerateReviewHandler(_db, _currentUser, _visibility);
    }

    private void SeedReasons()
    {
        _sets.RejectionReasons.AddRange(new[]
        {
            new RejectionReason
            {
                ReasonCode = "text_spam",
                Category = RejectionReasonCategory.Text,
                AdminLabel = "Spam",
                UserMessageTemplate = "Recenzja jest spamem.",
                IsActive = true
            },
            new RejectionReason
            {
                ReasonCode = "text_offtopic",
                Category = RejectionReasonCategory.Text,
                AdminLabel = "Off-topic",
                UserMessageTemplate = "Recenzja nie dotyczy lokalu.",
                IsActive = true
            },
            new RejectionReason
            {
                ReasonCode = "text_inactive",
                Category = RejectionReasonCategory.Text,
                AdminLabel = "Nieaktywny",
                UserMessageTemplate = "Nieaktywny powód.",
                IsActive = false
            },
            new RejectionReason
            {
                ReasonCode = "photo_nudity",
                Category = RejectionReasonCategory.Photo,
                AdminLabel = "Nagość",
                UserMessageTemplate = "Nagość.",
                IsActive = true
            }
        });
    }

    private Review SeedPendingReview()
    {
        var review = new ReviewBuilder().WithId(1).WithUserId(1)
            .WithContentStatus(ContentModerationStatus.Pending).Build();
        _sets.Reviews.Add(review);
        return review;
    }

    [Fact]
    public async Task Handle_Approve_SetsApprovedContentStatus()
    {
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Reject_WithSingleCode_ResolvesTemplateAndNotifies()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "text_spam" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
        review.ContentRejectionReason.Should().Be("Recenzja jest spamem.");
        _sets.Notifications.Should().ContainSingle();
        _sets.Notifications[0].Message.Should().Contain("Recenzja jest spamem.");
        _sets.ModerationLogs.Should().ContainSingle();
        _sets.ModerationLogs[0].ReasonCodes.Should().BeEquivalentTo(new[] { "text_spam" });
    }

    [Fact]
    public async Task Handle_Reject_WithMultipleCodes_JoinsTemplatesWithDoubleNewline()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "text_spam", "text_offtopic" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ContentRejectionReason.Should().Be("Recenzja jest spamem.\n\nRecenzja nie dotyczy lokalu.");
        _sets.ModerationLogs[0].ReasonCodes.Should().BeEquivalentTo(new[] { "text_spam", "text_offtopic" });
    }

    [Fact]
    public async Task Handle_Reject_WithModeratorNote_AppendsNoteAsExtraParagraph()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "text_spam" }, "Widać po stylu."),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ContentRejectionReason.Should().Be(
            "Recenzja jest spamem.\n\nDodatkowa uwaga moderatora: Widać po stylu.");
    }

    [Fact]
    public async Task Handle_Reject_WithOnlyModeratorNote_UsesNoteAsWholeMessage()
    {
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, null, "Nietypowy przypadek."),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ContentRejectionReason.Should().Be("Dodatkowa uwaga moderatora: Nietypowy przypadek.");
        _sets.ModerationLogs[0].ReasonCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Reject_WithNoCodeAndNoNote_ReturnsValidationError()
    {
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task Handle_Reject_WithUnknownCode_ReturnsValidationError()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "text_unknown" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_UNKNOWN_CODE");
    }

    [Fact]
    public async Task Handle_Reject_WithPhotoCode_ReturnsCategoryMismatch()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "photo_nudity" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_CATEGORY_MISMATCH");
    }

    [Fact]
    public async Task Handle_Reject_WithInactiveCode_ReturnsValidationError()
    {
        SeedReasons();
        var review = SeedPendingReview();
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, new[] { "text_inactive" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_INACTIVE");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ModerateReviewHandler(_db, nonAdmin, _visibility);

        var result = await handler.Handle(
            new ModerateReviewCommand(Guid.NewGuid(), true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ModerateReviewCommand(Guid.NewGuid(), true, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }
}
