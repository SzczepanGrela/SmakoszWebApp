using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reviews.Commands.CreateReview;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Commands.CreateReview;

[Trait("Category", "Handlers")]
public class CreateReviewHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly CreateReviewHandler _handler;

    public CreateReviewHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new CreateReviewHandler(_db, _currentUser, _forbiddenWords);
    }

    private CreateReviewCommand ValidCommand(Guid dishPublicId) => new(
        DishPublicId: dishPublicId,
        DishRating: 8,
        ServiceRating: 7,
        CleanlinessRating: 8,
        AmbianceRating: 7,
        Content: "Great food and excellent service!",
        VisitDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
    );

    private void SetupSaveChangesWithRequery()
    {
        int nextId = 100;
        _db.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                foreach (var r in _sets.Reviews.Where(r => r.ReviewId == 0))
                {
                    r.ReviewId = nextId++;
                    // Populate navigation properties for re-query (Include is no-op in LINQ-to-Objects)
                    r.User = _sets.Users.FirstOrDefault(u => u.UserId == r.UserId)!;
                    r.Dish = _sets.Dishes.FirstOrDefault(d => d.DishId == r.DishId)!;
                    r.Restaurant = _sets.Restaurants.FirstOrDefault(rest => rest.RestaurantId == r.RestaurantId)!;
                }
                DbContextMockFactory.Refresh(_db, _sets);
                return 1;
            });
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsReviewCard()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Users.Add(user);
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        SetupSaveChangesWithRequery();

        var result = await _handler.Handle(ValidCommand(dish.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.DishRating.Should().Be(8);
        result.Value.DishName.Should().Be(dish.DishName);
        result.Value.RestaurantName.Should().Be(restaurant.RestaurantName);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new CreateReviewHandler(_db, anonymousUser, _forbiddenWords);

        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var command = ValidCommand(Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AlreadyReviewed_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        var existingReview = new ReviewBuilder()
            .WithUserId(1).WithDishId(1).WithRestaurantId(1)
            .Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.Add(existingReview);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(ValidCommand(dish.PublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REVIEW_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_WithContent_SetsContentStatusPending()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Users.Add(user);
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        SetupSaveChangesWithRequery();
        var command = new CreateReviewCommand(dish.PublicId, 8, 7, 8, 7, "Great food!", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ContentStatus.Should().Be(ContentModerationStatus.Pending);
    }

    [Fact]
    public async Task Handle_NoContent_SetsContentStatusNone()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Users.Add(user);
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        SetupSaveChangesWithRequery();
        var command = new CreateReviewCommand(dish.PublicId, 8, 7, 8, 7, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ContentStatus.Should().Be(ContentModerationStatus.None);
    }

    [Fact]
    public async Task Handle_NonUserRole_ReturnsUserRoleOnlyError()
    {
        var restaurantUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "restaurant");
        var handler = new CreateReviewHandler(_db, restaurantUser, _forbiddenWords);

        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SOCIAL_USER_ROLE_ONLY");
    }
}
