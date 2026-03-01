using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetAdminIngredients;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAdminIngredients;

[Trait("Category", "Handlers")]
public class GetAdminIngredientsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAdminIngredientsHandler _handler;

    public GetAdminIngredientsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAdminIngredientsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedIngredients()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Salt" });
        _sets.Ingredients.Add(new Ingredient { IngredientId = 2, IngredientName = "Pepper" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminIngredientsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithSearch_FiltersResults()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Salt" });
        _sets.Ingredients.Add(new Ingredient { IngredientId = 2, IngredientName = "Pepper" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminIngredientsQuery(new PaginationParams(1, 20), "salt"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].IngredientName.Should().Be("Salt");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAdminIngredientsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetAdminIngredientsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
