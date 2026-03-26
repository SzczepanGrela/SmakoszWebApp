using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateIngredient;

public record UpdateIngredientCommand(
    int IngredientId,
    string? Name,
    bool? IsAllergen,
    bool? IsVegetarian,
    bool? IsVegan,
    bool? IsGlutenFree,
    bool? IsLactoseFree) : IRequest<ErrorOr<Success>>;

public class UpdateIngredientHandler : IRequestHandler<UpdateIngredientCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateIngredientHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateIngredientCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.IngredientId == request.IngredientId, cancellationToken);

        if (ingredient is null)
            return DomainErrors.Ingredient.NotFound;

        var oldValues = JsonSerializer.Serialize(new { ingredient.IngredientName, ingredient.IsAllergen, ingredient.IsVegetarian, ingredient.IsVegan, ingredient.IsGlutenFree, ingredient.IsLactoseFree });

        if (request.Name is not null)
            ingredient.IngredientName = request.Name;

        if (request.IsAllergen.HasValue)
            ingredient.IsAllergen = request.IsAllergen.Value;

        if (request.IsVegetarian.HasValue)
            ingredient.IsVegetarian = request.IsVegetarian.Value;

        if (request.IsVegan.HasValue)
            ingredient.IsVegan = request.IsVegan.Value;

        if (request.IsGlutenFree.HasValue)
            ingredient.IsGlutenFree = request.IsGlutenFree.Value;

        if (request.IsLactoseFree.HasValue)
            ingredient.IsLactoseFree = request.IsLactoseFree.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Ingredients",
            RecordId = ingredient.IngredientId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { ingredient.IngredientName, ingredient.IsAllergen, ingredient.IsVegetarian, ingredient.IsVegan, ingredient.IsGlutenFree, ingredient.IsLactoseFree })
        });

        // Recalculate dietary flags for all dishes that use this ingredient
        var affectedDishes = await _db.DishIngredients
            .Include(di => di.Dish)
            .Where(di => di.IngredientId == ingredient.IngredientId)
            .Select(di => di.Dish)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var dish in affectedDishes)
        {
            var dishIngredients = await _db.DishIngredients
                .Where(di => di.DishId == dish.DishId)
                .Select(di => di.Ingredient)
                .ToListAsync(cancellationToken);

            DishDietaryExtensions.RecalculateDietaryFlags(dish, dishIngredients);
            dish.IngredientsJson = DishDietaryExtensions.SerializeIngredientNames(dishIngredients);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
