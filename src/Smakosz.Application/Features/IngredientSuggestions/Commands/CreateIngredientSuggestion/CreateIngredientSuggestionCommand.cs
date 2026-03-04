using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.IngredientSuggestions.Commands.CreateIngredientSuggestion;

public record CreateIngredientSuggestionCommand(
    string RestaurantSlug,
    string SuggestedName,
    bool IsAllergen,
    bool IsVegetarian,
    bool IsVegan,
    bool IsGlutenFree,
    bool IsLactoseFree) : IRequest<ErrorOr<Success>>;

public class CreateIngredientSuggestionHandler : IRequestHandler<CreateIngredientSuggestionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateIngredientSuggestionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(CreateIngredientSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (_currentUser.Role is not "Restaurant" and not "Admin")
            return DomainErrors.Admin.Forbidden;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Slug == request.RestaurantSlug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var nameLower = request.SuggestedName.Trim().ToLowerInvariant();

        var existsInIngredients = await _db.Ingredients
            .AnyAsync(i => i.IngredientName.ToLower() == nameLower, cancellationToken);

        if (existsInIngredients)
            return DomainErrors.Ingredient.AlreadyExists;

        var existsPending = await _db.IngredientSuggestions
            .AnyAsync(s => s.SuggestedName.ToLower() == nameLower
                && s.Status == IngredientSuggestionStatus.Pending, cancellationToken);

        if (existsPending)
            return Error.Conflict("SUGGESTION_ALREADY_EXISTS", "Taka sugestia składnika już istnieje");

        var suggestion = new IngredientSuggestion
        {
            RestaurantId = restaurant.RestaurantId,
            UserId = _currentUser.UserId.Value,
            SuggestedName = request.SuggestedName.Trim(),
            IsAllergen = request.IsAllergen,
            IsVegetarian = request.IsVegetarian,
            IsVegan = request.IsVegan,
            IsGlutenFree = request.IsGlutenFree,
            IsLactoseFree = request.IsLactoseFree,
            Status = IngredientSuggestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.IngredientSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(cancellationToken);

        _db.SystemTickets.Add(new SystemTicket
        {
            TicketType = TicketType.IngredientSuggestion,
            ReferenceId = suggestion.SuggestionId,
            Status = TicketStatus.Open,
            Priority = 3,
            Description = $"Sugestia składnika \"{request.SuggestedName}\" dla restauracji \"{restaurant.RestaurantName}\""
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
