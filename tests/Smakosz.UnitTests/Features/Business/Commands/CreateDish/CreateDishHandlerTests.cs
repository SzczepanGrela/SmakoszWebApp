using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.CreateDish;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.CreateDish;

[Trait("Category", "Handlers")]
public class CreateDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly CreateDishHandler _handler;

    public CreateDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Business", sessionId: 100);
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new CreateDishHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesDishAndReturnsId()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Spaghetti Bolognese",
                Price: 28.99m,
                Description: "Classic Italian pasta",
                Calories: 650,
                IsAvailable: true,
                DishCategoryTagName: "Pizza",
                SectionIds: null,
                IngredientIds: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Dishes.Should().HaveCount(1);
        _sets.Dishes[0].DishName.Should().Be("Spaghetti Bolognese");
        _sets.Dishes[0].RestaurantId.Should().Be(10);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new CreateDishCommand("Pasta", 0m, null, null, true, "Pizza"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WithValidCategory_CreatesDishTagLinkingToTag()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Margherita",
                Price: 35m,
                Description: "Klasyczna pizza",
                Calories: 800,
                IsAvailable: true,
                DishCategoryTagName: "Pizza"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishTags.Should().ContainSingle(dt => dt.TagId == 1 && dt.DishId == _sets.Dishes[0].DishId);
    }

    [Fact]
    public async Task Handle_WithUnknownCategory_ReturnsInvalidCategoryError()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Margherita",
                Price: 35m,
                Description: null,
                Calories: null,
                IsAvailable: true,
                DishCategoryTagName: "NieistniejacaKategoria"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_INVALID_CATEGORY");
    }

}
