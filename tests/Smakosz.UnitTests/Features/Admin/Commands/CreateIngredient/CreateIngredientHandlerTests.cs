using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateIngredient;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateIngredient;

[Trait("Category", "Handlers")]
public class CreateIngredientHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly CreateIngredientHandler _handler;

    public CreateIngredientHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new CreateIngredientHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesIngredientAndReturnsId()
    {
        var result = await _handler.Handle(
            new CreateIngredientCommand("Gluten", true, false, false, false, false),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Ingredients.Should().HaveCount(1);
        _sets.Ingredients[0].IngredientName.Should().Be("Gluten");
        _sets.Ingredients[0].IsAllergen.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsAlreadyExistsError()
    {
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Gluten" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateIngredientCommand("gluten", true, false, false, false, false),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INGREDIENT_ALREADY_EXISTS");
    }
}
