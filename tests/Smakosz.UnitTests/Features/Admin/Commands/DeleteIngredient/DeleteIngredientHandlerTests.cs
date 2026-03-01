using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteIngredient;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteIngredient;

[Trait("Category", "Handlers")]
public class DeleteIngredientHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteIngredientHandler _handler;

    public DeleteIngredientHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new DeleteIngredientHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesIngredientAndAudits()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Salt" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteIngredientCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Ingredients.Should().BeEmpty();
        _sets.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new DeleteIngredientHandler(_db, nonAdmin);

        var result = await handler.Handle(new DeleteIngredientCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new DeleteIngredientCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INGREDIENT_NOT_FOUND");
    }
}
