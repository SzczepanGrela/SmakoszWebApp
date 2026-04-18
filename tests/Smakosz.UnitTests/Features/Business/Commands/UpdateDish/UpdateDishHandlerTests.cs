using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateDish;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateDish;

[Trait("Category", "Handlers")]
public class UpdateDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly UpdateDishHandler _handler;

    public UpdateDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateDishHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesNonTextFields()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, null, 19.99m, null, 600, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.Price.Should().Be(19.99m);
        dish.Calories.Should().Be(600);
        dish.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, "X", null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpdateDishCommand(Guid.NewGuid(), "X", null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_TextChange_CreatesEditRequest()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        dish.ModerationStatus = ContentModerationStatus.Approved;
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, "New Name", null, "New desc", null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.DishName.Should().Be("Test Dish"); // unchanged - text goes through EditRequest
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.RestaurantEditRequests.Should().HaveCount(1);
        var editRequest = _sets.RestaurantEditRequests[0];
        editRequest.NewName.Should().Be("New Name");
        editRequest.NewDescription.Should().Be("New desc");
        editRequest.ChangeScope.Should().Be(EditRequestChangeScope.Dish);
        editRequest.TargetEntityId.Should().Be(dish.DishId);
        editRequest.ModerationStatus.Should().Be(ContentModerationStatus.Pending);
    }

    [Fact]
    public async Task Handle_NonTextChangeOnly_DoesNotCreateEditRequest()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        dish.ModerationStatus = ContentModerationStatus.Approved;
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, null, 29.99m, null, 500, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        dish.Price.Should().Be(29.99m);
        dish.Calories.Should().Be(500);
        _sets.RestaurantEditRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForbiddenWordInName_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        _forbiddenWords.ContainsAsync("Bad Name", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, "Bad Name", null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
    }

    [Fact]
    public async Task Handle_ForbiddenWordInDescription_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        _forbiddenWords.ContainsAsync("Bad desc", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, null, null, "Bad desc", null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
    }

    [Fact]
    public async Task Handle_UpdateCategory_ReplacesExistingDishCategoryTag()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);

        var pizzaTag = new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        var burgerTag = new Tag { TagId = 2, TagName = "Burger", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        _sets.Tags.AddRange(new[] { pizzaTag, burgerTag });

        var publicId = Guid.NewGuid();
        var dish = new Dish { DishId = 100, DishName = "Margherita", RestaurantId = 10, PublicId = publicId, IsAvailable = true, Restaurant = restaurant };
        _sets.Dishes.Add(dish);
        var existing = new DishTag { DishId = 100, TagId = 1, Dish = dish, Tag = pizzaTag };
        dish.DishTags = new List<DishTag> { existing };
        _sets.DishTags.Add(existing);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(
                PublicId: publicId,
                Name: null,
                Price: null,
                Description: null,
                Calories: null,
                IsAvailable: null,
                DishCategoryTagName: "Burger"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishTags.Where(dt => dt.DishId == 100 && dt.Tag.Category == "dish_category")
            .Should().ContainSingle()
            .Which.TagId.Should().Be(2);
    }
}
