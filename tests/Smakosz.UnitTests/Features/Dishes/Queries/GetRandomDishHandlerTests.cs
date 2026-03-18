using FluentAssertions;
using Smakosz.Application.Features.Dishes.Queries.GetRandomDish;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Dishes.Queries;

[Trait("Category", "Handlers")]
public class GetRandomDishHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetRandomDishHandler _handler;

    public GetRandomDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetRandomDishHandler(_db);
    }

    [Fact]
    public async Task Handle_NoDishes_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetRandomDishQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    // Note: Happy path test skipped - EF.Functions.Random() throws InvalidOperationException
    // outside of EF query translation pipeline. Requires integration tests with real DB.
}
