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
    decimal? Price,
    string? Description,
    int? Calories,
    bool IsAvailable,
    string DishCategoryTagName,
    bool IsSpicy = false,
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

        var dish = new Dish
        {
            RestaurantId = restaurant.RestaurantId,
            DishName = request.Name,
            Price = request.Price,
            Description = request.Description,
            Calories = request.Calories,
            IsAvailable = request.IsAvailable,
            IsSpicy = request.IsSpicy,
            ModerationStatus = ContentModerationStatus.Pending,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Dishes.Add(dish);
        await _db.SaveChangesAsync(cancellationToken);

        _db.DishTags.Add(new DishTag
        {
            DishId = dish.DishId,
            TagId = categoryTag.TagId
        });
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
}

public class CreateDishValidator : AbstractValidator<CreateDishCommand>
{
    public CreateDishValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa dania jest wymagana")
            .MaximumLength(200).WithMessage("Nazwa dania może mieć maksymalnie 200 znaków");

        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("Cena nie może być ujemna");
        });

        RuleFor(x => x.DishCategoryTagName)
            .NotEmpty().WithMessage("Wybór kategorii dania jest wymagany");
    }
}
