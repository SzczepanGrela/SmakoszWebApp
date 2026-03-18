using FluentAssertions;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Queries.GetReviewsByUser;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Queries;

[Trait("Category", "Handlers")]
public class GetReviewsByUserHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetReviewsByUserHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public GetReviewsByUserHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetReviewsByUserHandler(_db, anonymousUser);
    }

    [Fact]
    public async Task Handle_ExistingUser_ReturnsReviews()
    {
        var user = new UserBuilder().WithId(1).WithSlug("reviewer").Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Users.Add(user);
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByUserQuery("reviewer", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new GetReviewsByUserQuery("nonexistent", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsNotFound()
    {
        var user = new UserBuilder().WithSlug("deleted-user").AsDeleted().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByUserQuery("deleted-user", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_LikedReviewsMarked()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 99);
        var handler = new GetReviewsByUserHandler(_db, authUser);
        var user = new UserBuilder().WithId(1).WithSlug("reviewer").Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).Build();
        var review = new ReviewBuilder()
            .WithId(1).WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Users.Add(user);
        _sets.Reviews.Add(review);
        _sets.ReviewLikes.Add(new ReviewLike { UserId = 99, ReviewId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(
            new GetReviewsByUserQuery("reviewer", _defaultPagination), CancellationToken.None);

        result.Value.Data[0].IsHelpfulByMe.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OnlyVisibleNonDeletedReviews_Returned()
    {
        var user = new UserBuilder().WithId(1).WithSlug("reviewer").Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).Build();
        var visible = new ReviewBuilder()
            .WithId(1).WithUser(user).WithDish(dish).WithRestaurant(restaurant).AsVisible().Build();
        var deleted = new ReviewBuilder()
            .WithId(2).WithUser(user).WithDish(dish).WithRestaurant(restaurant).AsDeleted().Build();
        var hidden = new ReviewBuilder()
            .WithId(3).WithUser(user).WithDish(dish).WithRestaurant(restaurant).AsHidden().Build();
        _sets.Users.Add(user);
        _sets.Reviews.AddRange(new[] { visible, deleted, hidden });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByUserQuery("reviewer", _defaultPagination), CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].PublicId.Should().Be(visible.PublicId);
    }
}
