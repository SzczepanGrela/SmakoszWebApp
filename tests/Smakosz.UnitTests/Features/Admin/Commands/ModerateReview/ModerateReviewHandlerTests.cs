using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ModerateReview;
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
    private readonly ModerateReviewHandler _handler;

    public ModerateReviewHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new ModerateReviewHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_Approve_SetsApprovedContentStatus()
    {
        var review = new ReviewBuilder().WithId(1).WithUserId(1)
            .WithContentStatus(ContentModerationStatus.Pending).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, true, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
    }

    [Fact]
    public async Task Handle_Reject_SetsRejectedAndNotifiesUser()
    {
        var review = new ReviewBuilder().WithId(1).WithUserId(1)
            .WithContentStatus(ContentModerationStatus.Pending).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ModerateReviewCommand(review.PublicId, false, "Inappropriate"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        review.ModerationStatus.Should().Be(ContentModerationStatus.Rejected);
        review.ContentRejectionReason.Should().Be("Inappropriate");
        _sets.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ModerateReviewHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ModerateReviewCommand(Guid.NewGuid(), true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ModerateReviewCommand(Guid.NewGuid(), true, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }
}
