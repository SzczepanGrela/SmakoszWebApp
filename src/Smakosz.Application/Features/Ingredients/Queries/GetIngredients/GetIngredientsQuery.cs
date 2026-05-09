using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Ingredients.Queries.GetIngredients;

public record GetIngredientsQuery() : IRequest<ErrorOr<List<IngredientDto>>>;

public class IngredientDto
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string? IconUrl { get; init; }
    public string? IconBlurhash { get; init; }
    public bool IsAllergen { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsLactoseFree { get; init; }
}
