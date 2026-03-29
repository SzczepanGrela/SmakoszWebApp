using System.Text.Json;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Common.Extensions;

public static class DishDietaryExtensions
{
    public static void RecalculateDietaryFlags(Dish dish, IReadOnlyList<Ingredient> ingredients)
    {
        if (ingredients.Count == 0)
        {
            dish.IsVegan = false;
            dish.IsVegetarian = false;
            dish.IsGlutenFree = false;
            dish.IsLactoseFree = false;
            return;
        }

        dish.IsVegan = ingredients.All(i => i.IsVegan);
        dish.IsVegetarian = ingredients.All(i => i.IsVegetarian);
        dish.IsGlutenFree = ingredients.All(i => i.IsGlutenFree);
        dish.IsLactoseFree = ingredients.All(i => i.IsLactoseFree);
    }

    public static string SerializeIngredientNames(IReadOnlyList<Ingredient> ingredients)
    {
        var names = ingredients.Select(i => i.IngredientName).ToList();
        return JsonSerializer.Serialize(names);
    }
}
