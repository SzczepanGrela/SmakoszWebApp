using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Extensions;
using Smakosz.Domain.Constants;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.CreateDish;

public record CreateDishCommand(
    string Name,
    decimal Price,
    string? Description,
    int? Calories,
    bool IsAvailable,
    string DishCategoryTagName,
    string? SpiceLevel = null,
    string? Mood = null,
    List<string>? Features = null,
    List<string>? Occasions = null,
    List<int>? SectionIds = null,
    List<int>? IngredientIds = null) : IRequest<ErrorOr<int>>;

public class CreateDishHandler : IRequestHandler<CreateDishCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public CreateDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<int>> Handle(CreateDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        if (request.Description is not null && await _forbiddenWords.ContainsAsync(request.Description, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var categoryTag = await _db.Tags
            .FirstOrDefaultAsync(t =>
                t.TagName == request.DishCategoryTagName
                && t.Category == TagCategories.DishCategory, cancellationToken);

        if (categoryTag is null)
            return DomainErrors.Dish.InvalidCategory;

        var extraTagIds = new List<int>();

        if (!string.IsNullOrWhiteSpace(request.SpiceLevel))
        {
            var spiceResult = await ResolveSingleTagIdAsync(TagCategories.Spice, request.SpiceLevel, cancellationToken);
            if (spiceResult.IsError) return spiceResult.Errors;
            extraTagIds.Add(spiceResult.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Mood))
        {
            var moodResult = await ResolveSingleTagIdAsync(TagCategories.Mood, request.Mood, cancellationToken);
            if (moodResult.IsError) return moodResult.Errors;
            extraTagIds.Add(moodResult.Value);
        }

        var featureIds = await ResolveMultipleTagIdsAsync(TagCategories.Feature, request.Features, cancellationToken);
        if (featureIds.IsError) return featureIds.Errors;
        extraTagIds.AddRange(featureIds.Value);

        var occasionIds = await ResolveMultipleTagIdsAsync(TagCategories.Occasion, request.Occasions, cancellationToken);
        if (occasionIds.IsError) return occasionIds.Errors;
        extraTagIds.AddRange(occasionIds.Value);

        var dish = new Dish
        {
            RestaurantId = restaurant.RestaurantId,
            DishName = request.Name,
            Price = request.Price,
            Description = request.Description,
            Calories = request.Calories,
            IsAvailable = request.IsAvailable,
            ModerationStatus = ContentModerationStatus.Pending,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Dishes.Add(dish);
        await _db.SaveChangesAsync(cancellationToken);

        _db.DishTags.Add(new DishTag { DishId = dish.DishId, TagId = categoryTag.TagId });
        foreach (var tid in extraTagIds.Distinct())
            _db.DishTags.Add(new DishTag { DishId = dish.DishId, TagId = tid });

        await _db.SaveChangesAsync(cancellationToken);

        if (request.SectionIds is { Count: > 0 })
        {
            var validSectionIds = await _db.MenuSections
                .Where(ms => ms.RestaurantId == restaurant.RestaurantId && request.SectionIds.Contains(ms.SectionId))
                .Select(ms => ms.SectionId)
                .ToListAsync(cancellationToken);

            for (var i = 0; i < validSectionIds.Count; i++)
            {
                _db.DishSectionAssignments.Add(new DishSectionAssignment
                {
                    DishId = dish.DishId,
                    SectionId = validSectionIds[i],
                    DisplayOrder = i + 1,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        if (request.IngredientIds is { Count: > 0 })
        {
            var ingredients = await _db.Ingredients
                .Where(i => request.IngredientIds.Contains(i.IngredientId))
                .ToListAsync(cancellationToken);

            foreach (var ingredient in ingredients)
            {
                _db.DishIngredients.Add(new DishIngredient
                {
                    DishId = dish.DishId,
                    IngredientId = ingredient.IngredientId
                });
            }

            DishDietaryExtensions.RecalculateDietaryFlags(dish, ingredients);
            dish.IngredientsJson = DishDietaryExtensions.SerializeIngredientNames(ingredients);

            await _db.SaveChangesAsync(cancellationToken);
        }

        return dish.DishId;
    }

    private async Task<ErrorOr<int>> ResolveSingleTagIdAsync(string category, string name, CancellationToken ct)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(
            t => t.Category == category && t.TagName == name, ct);
        if (tag is null) return DomainErrors.Dish.InvalidTag(category, name);
        return tag.TagId;
    }

    private async Task<ErrorOr<List<int>>> ResolveMultipleTagIdsAsync(string category, List<string>? names, CancellationToken ct)
    {
        if (names is null || names.Count == 0) return new List<int>();
        var distinct = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (distinct.Count == 0) return new List<int>();

        var found = await _db.Tags
            .Where(t => t.Category == category && distinct.Contains(t.TagName))
            .Select(t => new { t.TagId, t.TagName })
            .ToListAsync(ct);

        var missing = distinct.Except(found.Select(f => f.TagName)).FirstOrDefault();
        if (missing is not null) return DomainErrors.Dish.InvalidTag(category, missing);

        return found.Select(f => f.TagId).ToList();
    }
}

public class CreateDishValidator : AbstractValidator<CreateDishCommand>
{
    public CreateDishValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa dania jest wymagana")
            .MaximumLength(200).WithMessage("Nazwa dania może mieć maksymalnie 200 znaków");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Cena nie może być ujemna");

        RuleFor(x => x.DishCategoryTagName)
            .NotEmpty().WithMessage("Wybór kategorii dania jest wymagany");

        When(x => !string.IsNullOrEmpty(x.SpiceLevel), () =>
        {
            RuleFor(x => x.SpiceLevel!)
                .Must(v => SpiceLevels.All.Contains(v))
                .WithMessage("Nieprawidłowa wartość ostrości");
        });
    }
}
