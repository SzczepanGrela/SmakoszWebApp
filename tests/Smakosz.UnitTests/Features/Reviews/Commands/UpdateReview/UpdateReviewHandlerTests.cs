using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Commands.UpdateReview;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Commands.UpdateReview;

[Trait("Category", "Handlers")]
public class UpdateReviewHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateReviewHandler _handler;

    public UpdateReviewHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new UpdateReviewHandler(_db, _currentUser);
    }

    private UpdateReviewCommand ValidCommand(Guid reviewPublicId) => new(
        ReviewPublicId: reviewPublicId,
        DishRating: 9,
        ServiceRating: 8,
        CleanlinessRating: 9,
        AmbianceRating: 8,
        Content: "Updated review content here!",
        VisitDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
    );

    [Fact]
    public async Task Handle_ValidCommand_UpdatesReview()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithDishRating(5)
            .Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = ValidCommand(review.PublicId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.DishRating.Should().Be(9);
        result.Value.ServiceRating.Should().Be(8);
        result.Value.Content.Should().Be("Updated review content here!");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new UpdateReviewHandler(_db, anonymousUser);

        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ReturnsError()
    {
        var command = ValidCommand(Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var otherUser = new UserBuilder().WithId(99).Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(otherUser).WithDish(dish).WithRestaurant(restaurant)
            .Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(ValidCommand(review.PublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_UpdateContent_SetsContentStatusPending()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithContentStatus(ContentModerationStatus.None)
            .Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new UpdateReviewCommand(review.PublicId, 8, 7, 8, 7, "New content added!", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ContentStatus.Should().Be(ContentModerationStatus.Pending);
    }

    [Fact]
    public async Task Handle_ClearContent_SetsContentStatusNone()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithContent("Some content")
            .WithContentStatus(ContentModerationStatus.Approved)
            .Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new UpdateReviewCommand(review.PublicId, 8, 7, 8, 7, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ContentStatus.Should().Be(ContentModerationStatus.None);
    }
}
