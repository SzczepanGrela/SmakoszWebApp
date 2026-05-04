using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;
using AppSearch = Smakosz.Application.Features.Search.Queries.SearchQuery;

namespace Smakosz.UnitTests.Features.Search.Queries.SearchQuery;

[Trait("Category", "Handlers")]
public class SearchHandlerDishCategoryTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly AppSearch.SearchHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public SearchHandlerDishCategoryTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var config = Substitute.For<IPublicConfigProvider>();
        config.GetDoubleAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<double>(1)));
        _handler = new AppSearch.SearchHandler(_db, anonymousUser, config);
    }

    [Fact]
    public async Task Handle_FilterByDishCategoryPizza_ReturnsOnlyPizzaDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();

        var tagPizza = new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        var tagBurger = new Tag { TagId = 2, TagName = "Burger", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };

        var margherita = new DishBuilder().WithId(1).WithName("Margherita").WithRestaurant(restaurant).Build();
        var burger = new DishBuilder().WithId(2).WithName("Classic Burger").WithRestaurant(restaurant).Build();

        var dtPizza = new DishTag { DishId = 1, TagId = 1, Dish = margherita, Tag = tagPizza };
        var dtBurger = new DishTag { DishId = 2, TagId = 2, Dish = burger, Tag = tagBurger };

        margherita.DishTags = new List<DishTag> { dtPizza };
        burger.DishTags = new List<DishTag> { dtBurger };

        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { margherita, burger });
        _sets.Tags.AddRange(new[] { tagPizza, tagBurger });
        _sets.DishTags.AddRange(new[] { dtPizza, dtBurger });
        DbContextMockFactory.Refresh(_db, _sets);

        var query = new AppSearch.SearchQuery(_defaultPagination, Type: "dishes", DishCategories: "Pizza");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Dishes[0].DishName.Should().Be("Margherita");
    }

    [Fact]
    public async Task Handle_FilterByMultipleCategoriesOrSemantic_ReturnsBoth()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();

        var tagPizza = new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        var tagBurger = new Tag { TagId = 2, TagName = "Burger", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        var tagZupa = new Tag { TagId = 3, TagName = "Zupa", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };

        var margherita = new DishBuilder().WithId(1).WithName("Margherita").WithRestaurant(restaurant).Build();
        var burger = new DishBuilder().WithId(2).WithName("Classic Burger").WithRestaurant(restaurant).Build();
        var zupa = new DishBuilder().WithId(3).WithName("Zupa pomidorowa").WithRestaurant(restaurant).Build();

        var dtPizza = new DishTag { DishId = 1, TagId = 1, Dish = margherita, Tag = tagPizza };
        var dtBurger = new DishTag { DishId = 2, TagId = 2, Dish = burger, Tag = tagBurger };
        var dtZupa = new DishTag { DishId = 3, TagId = 3, Dish = zupa, Tag = tagZupa };

        margherita.DishTags = new List<DishTag> { dtPizza };
        burger.DishTags = new List<DishTag> { dtBurger };
        zupa.DishTags = new List<DishTag> { dtZupa };

        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { margherita, burger, zupa });
        _sets.Tags.AddRange(new[] { tagPizza, tagBurger, tagZupa });
        _sets.DishTags.AddRange(new[] { dtPizza, dtBurger, dtZupa });
        DbContextMockFactory.Refresh(_db, _sets);

        var query = new AppSearch.SearchQuery(_defaultPagination, Type: "dishes", DishCategories: "Pizza,Burger");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Dishes.Should().HaveCount(2);
        result.Value.Dishes.Select(d => d.DishName).Should().Contain("Margherita").And.Contain("Classic Burger");
        result.Value.Dishes.Select(d => d.DishName).Should().NotContain("Zupa pomidorowa");
    }

    [Fact]
    public async Task Handle_FilterByCategoryAndTag_AndSemantic()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();

        var tagPizza = new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish };
        var tagNaWynos = new Tag { TagId = 2, TagName = "Na wynos", Category = "service", TargetEntity = TagTargetEntity.Dish };

        var pizzaWithTakeout = new DishBuilder().WithId(1).WithName("Pizza Na Wynos").WithRestaurant(restaurant).Build();
        var pizzaWithoutTakeout = new DishBuilder().WithId(2).WithName("Pizza Na Miejscu").WithRestaurant(restaurant).Build();

        var dtPizza1 = new DishTag { DishId = 1, TagId = 1, Dish = pizzaWithTakeout, Tag = tagPizza };
        var dtNaWynos = new DishTag { DishId = 1, TagId = 2, Dish = pizzaWithTakeout, Tag = tagNaWynos };
        var dtPizza2 = new DishTag { DishId = 2, TagId = 1, Dish = pizzaWithoutTakeout, Tag = tagPizza };

        pizzaWithTakeout.DishTags = new List<DishTag> { dtPizza1, dtNaWynos };
        pizzaWithoutTakeout.DishTags = new List<DishTag> { dtPizza2 };

        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { pizzaWithTakeout, pizzaWithoutTakeout });
        _sets.Tags.AddRange(new[] { tagPizza, tagNaWynos });
        _sets.DishTags.AddRange(new[] { dtPizza1, dtNaWynos, dtPizza2 });
        DbContextMockFactory.Refresh(_db, _sets);

        var query = new AppSearch.SearchQuery(_defaultPagination, Type: "dishes", DishCategories: "Pizza", Tags: "Na wynos");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Dishes[0].DishName.Should().Be("Pizza Na Wynos");
    }

    [Fact]
    public async Task Handle_DishWithCategory_PopulatesCategoryTagNameInCardDto()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var tagPizza = new Tag { TagId = 1, TagName = "Pizza", Category = "dish_category", TargetEntity = TagTargetEntity.Dish, DisplayColor = "#ff5722" };

        var margherita = new DishBuilder().WithId(1).WithName("Margherita").WithRestaurant(restaurant).Build();
        var dt = new DishTag { DishId = 1, TagId = 1, Dish = margherita, Tag = tagPizza };
        margherita.DishTags = new List<DishTag> { dt };

        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(margherita);
        _sets.Tags.Add(tagPizza);
        _sets.DishTags.Add(dt);
        DbContextMockFactory.Refresh(_db, _sets);

        var query = new AppSearch.SearchQuery(_defaultPagination, Type: "dishes");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Dishes[0].CategoryTagName.Should().Be("Pizza");
        result.Value.Dishes[0].CategoryColor.Should().Be("#ff5722");
    }
}
