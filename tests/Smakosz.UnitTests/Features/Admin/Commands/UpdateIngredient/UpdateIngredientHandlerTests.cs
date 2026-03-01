using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateIngredient;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateIngredient;

[Trait("Category", "Handlers")]
public class UpdateIngredientHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateIngredientHandler _handler;

    public UpdateIngredientHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new UpdateIngredientHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesAndRecalculatesDietaryFlags()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Flour" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateIngredientCommand(1, "Wheat Flour", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Ingredients[0].IngredientName.Should().Be("Wheat Flour");
        _sets.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateIngredientHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new UpdateIngredientCommand(1, "X", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpdateIngredientCommand(999, "X", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INGREDIENT_NOT_FOUND");
    }
}
