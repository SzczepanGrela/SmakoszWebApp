using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateDish;
using Smakosz.Domain.Constants;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateDish;

[Trait("Category", "Handlers")]
public class UpdateDishTaxonomyTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly UpdateDishHandler _handler;
    private readonly Guid _publicId = Guid.NewGuid();

    public UpdateDishTaxonomyTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Business", sessionId: 100);
        var forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateDishHandler(_db, currentUser, forbiddenWords);

        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);

        var moodRomantic = new Tag { TagId = 10, TagName = "Romantyczne", Category = TagCategories.Mood };
        var moodCasual = new Tag { TagId = 11, TagName = "Casual", Category = TagCategories.Mood };
        var spiceHot = new Tag { TagId = 20, TagName = SpiceLevels.Hot, Category = TagCategories.Spice };
        var featSeasonal = new Tag { TagId = 30, TagName = "Sezonowe", Category = TagCategories.Feature };
        _sets.Tags.AddRange(new[] { moodRomantic, moodCasual, spiceHot, featSeasonal });

        var dish = new Dish
        {
            DishId = 100,
            PublicId = _publicId,
            RestaurantId = 10,
            Restaurant = restaurant,
            DishName = "Dish",
            IsAvailable = true,
            DishTags =
            [
                new DishTag { DishId = 100, TagId = 10, Tag = moodRomantic },
                new DishTag { DishId = 100, TagId = 20, Tag = spiceHot },
                new DishTag { DishId = 100, TagId = 30, Tag = featSeasonal }
            ]
        };
        _sets.Dishes.Add(dish);
        _sets.DishTags.AddRange(dish.DishTags);
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_NewMood_ReplacesOldMood()
    {
        var result = await _handler.Handle(
            new UpdateDishCommand(
                PublicId: _publicId,
                Name: null,
                Price: null,
                Description: null,
                Calories: null,
                IsAvailable: null,
                Mood: "Casual"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishTags.Should().NotContain(dt => dt.TagId == 10);
        _sets.DishTags.Should().Contain(dt => dt.TagId == 11);
    }

    [Fact]
    public async Task Handle_EmptyFeaturesList_ClearsAllFeatures()
    {
        var result = await _handler.Handle(
            new UpdateDishCommand(
                PublicId: _publicId,
                Name: null,
                Price: null,
                Description: null,
                Calories: null,
                IsAvailable: null,
                Features: []),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishTags.Should().NotContain(dt => dt.TagId == 30);
    }

    [Fact]
    public async Task Handle_NullSpiceLevel_PreservesExisting()
    {
        var result = await _handler.Handle(
            new UpdateDishCommand(
                PublicId: _publicId,
                Name: null,
                Price: null,
                Description: null,
                Calories: null,
                IsAvailable: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishTags.Should().Contain(dt => dt.TagId == 20);
    }
}
