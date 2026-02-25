using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetBusinessDishDetail;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessDishDetail;

[Trait("Category", "Handlers")]
public class GetBusinessDishDetailHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessDishDetailHandler _handler;
    private static readonly Guid TestDishPublicId = Guid.NewGuid();

    public GetBusinessDishDetailHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new GetBusinessDishDetailHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsDishDetail()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "R", Slug = "r" };
        var dish = new Dish
        {
            DishId = 5,
            PublicId = TestDishPublicId,
            RestaurantId = 1,
            DishName = "Pizza",
            Slug = "pizza",
            Price = 25.00m,
            IsAvailable = true,
            Restaurant = restaurant
        };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDishDetailQuery(TestDishPublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.DishName.Should().Be("Pizza");
        result.Value.PublicId.Should().Be(TestDishPublicId);
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new GetBusinessDishDetailQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 99, RestaurantName = "R", Slug = "r" };
        var dish = new Dish
        {
            DishId = 5,
            PublicId = TestDishPublicId,
            RestaurantId = 1,
            DishName = "Pizza",
            Slug = "pizza",
            Restaurant = restaurant
        };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDishDetailQuery(TestDishPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }
}
