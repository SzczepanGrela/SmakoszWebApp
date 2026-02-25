using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Ingredients.Queries.GetIngredients;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Ingredients.Queries.GetIngredients;

[Trait("Category", "Handlers")]
public class GetIngredientsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetIngredientsHandler _handler;

    public GetIngredientsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetIngredientsHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsAllIngredients()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Mąka", IsAllergen = false });
        _sets.Ingredients.Add(new Ingredient { IngredientId = 2, IngredientName = "Gluten", IsAllergen = true });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetIngredientsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_EmptyDb_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetIngredientsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
