using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Ingredients.Queries.GetIngredients;

public class GetIngredientsHandler : IRequestHandler<GetIngredientsQuery, ErrorOr<List<IngredientDto>>>
{
    private readonly ISmakoszDbContext _db;

    public GetIngredientsHandler(ISmakoszDbContext db) => _db = db;

    public async Task<ErrorOr<List<IngredientDto>>> Handle(GetIngredientsQuery request, CancellationToken cancellationToken)
    {
        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .OrderBy(i => i.IngredientName)
            .Select(i => new IngredientDto
            {
                Id = i.IngredientId,
                Name = i.IngredientName,
                IconUrl = i.IconUrl,
                IsAllergen = i.IsAllergen,
                IsVegetarian = i.IsVegetarian,
                IsVegan = i.IsVegan,
                IsGlutenFree = i.IsGlutenFree,
                IsLactoseFree = i.IsLactoseFree
            })
            .ToListAsync(cancellationToken);

        return ingredients;
    }
}
