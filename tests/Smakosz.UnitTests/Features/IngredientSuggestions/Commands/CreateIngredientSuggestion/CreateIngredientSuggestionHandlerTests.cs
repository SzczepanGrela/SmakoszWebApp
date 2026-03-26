using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.IngredientSuggestions.Commands.CreateIngredientSuggestion;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.IngredientSuggestions.Commands.CreateIngredientSuggestion;

[Trait("Category", "Handlers")]
public class CreateIngredientSuggestionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly CreateIngredientSuggestionHandler _handler;

    public CreateIngredientSuggestionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new CreateIngredientSuggestionHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesSuggestionAndTicket()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateIngredientSuggestionCommand("bella-italia", "Tofu", false, true, true, true, true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.IngredientSuggestions.Should().HaveCount(1);
        _sets.SystemTickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_IngredientAlreadyExists_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Tofu" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateIngredientSuggestionCommand("bella-italia", "tofu", false, true, true, true, true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INGREDIENT_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_DuplicatePendingSuggestion_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        _sets.Restaurants.Add(restaurant);
        _sets.IngredientSuggestions.Add(new IngredientSuggestion
        {
            SuggestionId = 1, SuggestedName = "Tofu", Status = IngredientSuggestionStatus.Pending,
            RestaurantId = 1, UserId = 2
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateIngredientSuggestionCommand("bella-italia", "tofu", false, true, true, true, true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SUGGESTION_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new CreateIngredientSuggestionCommand("nonexistent", "Tofu", false, true, true, true, true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
