using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Commands.DeleteReview;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Commands.DeleteReview;

[Trait("Category", "Handlers")]
public class DeleteReviewHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteReviewHandler _handler;

    public DeleteReviewHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new DeleteReviewHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCommand_RemovesReview()
    {
        var review = new ReviewBuilder().WithUserId(1).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new DeleteReviewCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new DeleteReviewHandler(_db, anonymousUser);

        var result = await handler.Handle(new DeleteReviewCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ReturnsError()
    {
        var command = new DeleteReviewCommand(Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var review = new ReviewBuilder().WithUserId(99).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new DeleteReviewCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_DeletedReview_ReturnsNotFound()
    {
        var review = new ReviewBuilder().WithUserId(1).AsDeleted().Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new DeleteReviewCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }
}
