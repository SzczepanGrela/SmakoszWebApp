using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Commands.ToggleReviewLike;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Commands.ToggleReviewLike;

[Trait("Category", "Handlers")]
public class ToggleReviewLikeHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ToggleReviewLikeHandler _handler;

    public ToggleReviewLikeHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new ToggleReviewLikeHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsAuthError()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new ToggleReviewLikeHandler(_db, anonymousUser);

        var result = await handler.Handle(new ToggleReviewLikeCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenReviewNotFound_ReturnsNotFoundError()
    {
        var command = new ToggleReviewLikeCommand(Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenLikingOwnReview_ReturnsError()
    {
        var review = new ReviewBuilder().WithUserId(1).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ToggleReviewLikeCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_LIKE_CANNOT_LIKE_OWN");
    }

    [Fact]
    public async Task Handle_WhenNotYetLiked_AddsLikeAndIncrementsCount()
    {
        var review = new ReviewBuilder().WithUserId(99).WithHelpfulCount(2).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ToggleReviewLikeCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsLiked.Should().BeTrue();
        result.Value.HelpfulCount.Should().Be(3);
        _sets.ReviewLikes.Should().ContainSingle(l => l.UserId == 1 && l.ReviewId == review.ReviewId);
        await _db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyLiked_RemovesLikeAndDecrementsCount()
    {
        var review = new ReviewBuilder().WithUserId(99).WithHelpfulCount(3).Build();
        var existingLike = new ReviewLike { UserId = 1, ReviewId = review.ReviewId, CreatedAt = DateTime.UtcNow };
        _sets.Reviews.Add(review);
        _sets.ReviewLikes.Add(existingLike);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ToggleReviewLikeCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsLiked.Should().BeFalse();
        result.Value.HelpfulCount.Should().Be(2);
        await _db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLiked_CreatesNotificationForReviewAuthor()
    {
        var review = new ReviewBuilder().WithUserId(99).WithHelpfulCount(0).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new ToggleReviewLikeCommand(review.PublicId), CancellationToken.None);

        _sets.Notifications.Should().ContainSingle(n =>
            n.UserId == 99
            && n.ActorId == 1
            && n.Type == NotificationType.Like
            && n.GroupKey == $"like:review:{review.ReviewId}");
    }

    [Fact]
    public async Task Handle_WhenUnliked_DoesNotCreateNotification()
    {
        var review = new ReviewBuilder().WithUserId(99).WithHelpfulCount(1).Build();
        var existingLike = new ReviewLike { UserId = 1, ReviewId = review.ReviewId, CreatedAt = DateTime.UtcNow };
        _sets.Reviews.Add(review);
        _sets.ReviewLikes.Add(existingLike);
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new ToggleReviewLikeCommand(review.PublicId), CancellationToken.None);

        _sets.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenLikedAgain_GroupsNotification()
    {
        var review = new ReviewBuilder().WithUserId(99).WithHelpfulCount(1).Build();
        var existingNotification = new Notification
        {
            UserId = 99,
            ActorId = 50,
            Type = NotificationType.Like,
            GroupKey = $"like:review:{review.ReviewId}",
            Counter = 1,
            IsRead = false,
            Title = "Polubienie recenzji",
            Message = "Ktoś polubił Twoją recenzję.",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _sets.Reviews.Add(review);
        _sets.Notifications.Add(existingNotification);
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new ToggleReviewLikeCommand(review.PublicId), CancellationToken.None);

        _sets.Notifications.Should().HaveCount(1);
        existingNotification.Counter.Should().Be(2);
        existingNotification.ActorId.Should().Be(1);
    }
}
