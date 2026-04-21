using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;
using Smakosz.Domain.Constants;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessDishDetail;

public record GetBusinessDishDetailQuery(Guid PublicId) : IRequest<ErrorOr<BusinessDishDetailDto>>;

public class BusinessDishDetailDto
{
    public Guid PublicId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public int? Calories { get; set; }
    public bool IsAvailable { get; set; }
    public string? ImageUrl { get; set; }
    public string? IngredientsJson { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
    public string? SpiceLevel { get; set; }
    public string? Mood { get; set; }
    public List<string> Features { get; set; } = new();
    public List<string> Occasions { get; set; } = new();
    public int ReviewCount { get; set; }
    public double? AvgRating { get; set; }
    public string? CategoryTagName { get; set; }
    public List<DishIngredientItemDto> Ingredients { get; set; } = new();
}

public class DishIngredientItemDto
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAllergen { get; set; }
}

public class GetBusinessDishDetailHandler : IRequestHandler<GetBusinessDishDetailQuery, ErrorOr<BusinessDishDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessDishDetailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<BusinessDishDetailDto>> Handle(GetBusinessDishDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Restaurant)
            .Include(d => d.DishIngredients)
                .ThenInclude(di => di.Ingredient)
            .Include(d => d.DishTags)
                .ThenInclude(dt => dt.Tag)
            .Where(d => d.PublicId == request.PublicId)
            .Select(d => new
            {
                d.PublicId,
                d.DishName,
                d.Slug,
                d.Price,
                d.Description,
                d.Calories,
                d.IsAvailable,
                d.ImageUrl,
                d.IngredientsJson,
                d.IsVegetarian,
                d.IsVegan,
                d.IsGlutenFree,
                d.IsLactoseFree,
                d.ReviewCount,
                d.AvgRating,
                OwnerId = d.Restaurant!.OwnerId,
                CategoryTagName = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.DishCategory)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                SpiceLevel = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Spice)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                Mood = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Mood)
                    .Select(dt => dt.Tag.TagName)
                    .FirstOrDefault(),
                Features = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Feature)
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                Occasions = d.DishTags
                    .Where(dt => dt.Tag.Category == TagCategories.Occasion)
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                Ingredients = d.DishIngredients.Select(di => new DishIngredientItemDto
                {
                    IngredientId = di.IngredientId,
                    Name = di.Ingredient.IngredientName,
                    IsAllergen = di.Ingredient.IsAllergen
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        return new BusinessDishDetailDto
        {
            PublicId = dish.PublicId,
            DishName = dish.DishName,
            Slug = dish.Slug,
            Price = dish.Price,
            Description = dish.Description,
            Calories = dish.Calories,
            IsAvailable = dish.IsAvailable,
            ImageUrl = dish.ImageUrl,
            IngredientsJson = dish.IngredientsJson,
            IsVegetarian = dish.IsVegetarian,
            IsVegan = dish.IsVegan,
            IsGlutenFree = dish.IsGlutenFree,
            IsLactoseFree = dish.IsLactoseFree,
            SpiceLevel = dish.SpiceLevel,
            Mood = dish.Mood,
            Features = dish.Features,
            Occasions = dish.Occasions,
            ReviewCount = dish.ReviewCount,
            AvgRating = dish.AvgRating,
            CategoryTagName = dish.CategoryTagName,
            Ingredients = dish.Ingredients
        };
    }
}
