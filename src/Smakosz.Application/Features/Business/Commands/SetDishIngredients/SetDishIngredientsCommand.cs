using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Business.Commands.SetDishIngredients;

public record SetDishIngredientsCommand(Guid PublicId, List<int> IngredientIds) : IRequest<ErrorOr<Success>>;

public class SetDishIngredientsHandler : IRequestHandler<SetDishIngredientsCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SetDishIngredientsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(SetDishIngredientsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .Include(d => d.Restaurant)
            .Include(d => d.DishIngredients)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.Restaurant?.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        var ingredients = await _db.Ingredients
            .Where(i => request.IngredientIds.Contains(i.IngredientId))
            .ToListAsync(cancellationToken);

        if (ingredients.Count != request.IngredientIds.Count)
            return Error.Validation("INVALID_INGREDIENTS", "Some ingredient IDs are invalid.");

        // Remove old assignments
        foreach (var old in dish.DishIngredients.ToList())
            _db.DishIngredients.Remove(old);

        // Add new assignments
        foreach (var ingredient in ingredients)
        {
            _db.DishIngredients.Add(new DishIngredient
            {
                DishId = dish.DishId,
                IngredientId = ingredient.IngredientId
            });
        }

        // Recalculate dietary flags
        DishDietaryExtensions.RecalculateDietaryFlags(dish, ingredients);

        // Serialize ingredient names to JSON
        dish.IngredientsJson = DishDietaryExtensions.SerializeIngredientNames(ingredients);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
