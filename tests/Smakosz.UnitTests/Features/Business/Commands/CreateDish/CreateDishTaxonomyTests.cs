using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.CreateDish;
using Smakosz.Domain.Constants;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.CreateDish;

[Trait("Category", "Handlers")]
public class CreateDishTaxonomyTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly CreateDishHandler _handler;

    public CreateDishTaxonomyTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Business", sessionId: 100);
        var forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new CreateDishHandler(_db, currentUser, forbiddenWords);

        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        _sets.Tags.Add(new Tag { TagId = 1, TagName = "Pizza", Category = TagCategories.DishCategory, TargetEntity = TagTargetEntity.Dish });
        _sets.Tags.Add(new Tag { TagId = 2, TagName = SpiceLevels.Hot, Category = TagCategories.Spice, TargetEntity = TagTargetEntity.Dish });
        _sets.Tags.Add(new Tag { TagId = 3, TagName = "Romantyczne", Category = TagCategories.Mood, TargetEntity = TagTargetEntity.Dish });
        _sets.Tags.Add(new Tag { TagId = 4, TagName = "Sezonowe", Category = TagCategories.Feature, TargetEntity = TagTargetEntity.Dish });
        _sets.Tags.Add(new Tag { TagId = 5, TagName = "Fusion", Category = TagCategories.Feature, TargetEntity = TagTargetEntity.Dish });
        _sets.Tags.Add(new Tag { TagId = 6, TagName = "Kolacja", Category = TagCategories.Occasion, TargetEntity = TagTargetEntity.Dish });
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_WithSpiceLevel_CreatesSpiceTag()
    {
        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Kebab ostry",
                Price: 25m,
                Description: null,
                Calories: null,
                IsAvailable: true,
                DishCategoryTagName: "Pizza",
                SpiceLevel: SpiceLevels.Hot),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var dishId = _sets.Dishes[0].DishId;
        _sets.DishTags.Should().Contain(dt => dt.DishId == dishId && dt.TagId == 2);
    }

    [Fact]
    public async Task Handle_WithFeatures_CreatesMultipleFeatureTags()
    {
        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Pizza",
                Price: 35m,
                Description: null,
                Calories: null,
                IsAvailable: true,
                DishCategoryTagName: "Pizza",
                Features: ["Sezonowe", "Fusion"]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var dishId = _sets.Dishes[0].DishId;
        _sets.DishTags.Count(dt => dt.DishId == dishId && (dt.TagId == 4 || dt.TagId == 5)).Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithInvalidSpiceLevel_ReturnsInvalidTagError()
    {
        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Pizza",
                Price: 35m,
                Description: null,
                Calories: null,
                IsAvailable: true,
                DishCategoryTagName: "Pizza",
                SpiceLevel: "Niepasujacy"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_INVALID_TAG");
    }

    [Fact]
    public async Task Handle_AllTagFieldsNull_CreatesOnlyDishCategoryTag()
    {
        var result = await _handler.Handle(
            new CreateDishCommand(
                Name: "Pizza",
                Price: 35m,
                Description: null,
                Calories: null,
                IsAvailable: true,
                DishCategoryTagName: "Pizza"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var dishId = _sets.Dishes[0].DishId;
        _sets.DishTags.Should().ContainSingle(dt => dt.DishId == dishId).Which.TagId.Should().Be(1);
    }
}
