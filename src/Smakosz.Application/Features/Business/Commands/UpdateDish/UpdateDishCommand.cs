using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Extensions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.UpdateDish;

public record UpdateDishCommand(
    Guid PublicId,
    string? Name,
    decimal? Price,
    string? Description,
    int? Calories,
    bool? IsAvailable,
    string? DishCategoryTagName = null,
    List<int>? IngredientIds = null,
    List<int>? SectionIds = null) : IRequest<ErrorOr<Success>>;

public class UpdateDishHandler : IRequestHandler<UpdateDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public UpdateDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var query = _db.Dishes
            .Include(d => d.Restaurant)
            .AsQueryable();

        if (request.IngredientIds is not null)
            query = query.Include(d => d.DishIngredients);

        if (request.DishCategoryTagName is not null)
            query = query.Include(d => d.DishTags).ThenInclude(dt => dt.Tag);

        var dish = await query
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.Restaurant?.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        if (request.DishCategoryTagName is not null)
        {
            var newCategoryTag = await _db.Tags
                .FirstOrDefaultAsync(t =>
                    t.TagName == request.DishCategoryTagName
                    && t.Category == "dish_category", cancellationToken);

            if (newCategoryTag is null)
                return DomainErrors.Dish.InvalidCategory;

            var oldCategoryTags = dish.DishTags
                .Where(dt => dt.Tag.Category == "dish_category")
                .ToList();
            foreach (var old in oldCategoryTags)
                _db.DishTags.Remove(old);

            _db.DishTags.Add(new DishTag
            {
                DishId = dish.DishId,
                TagId = newCategoryTag.TagId,
                Tag = newCategoryTag
            });
        }

        if (request.Price.HasValue) dish.Price = request.Price.Value;
        if (request.Calories.HasValue) dish.Calories = request.Calories.Value;
        if (request.IsAvailable.HasValue) dish.IsAvailable = request.IsAvailable.Value;

        if (request.Name is not null && await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        if (request.Description is not null && await _forbiddenWords.ContainsAsync(request.Description, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        if (request.Name is not null || request.Description is not null)
        {
            var editRequest = new RestaurantEditRequest
            {
                RestaurantId = dish.Restaurant!.RestaurantId,
                UserId = _currentUser.UserId.Value,
                ChangeType = EditRequestChangeType.DishUpdate,
                ChangeScope = EditRequestChangeScope.Dish,
                TargetEntityId = dish.DishId,
                Payload = "{}",
                NewName = request.Name,
                NewDescription = request.Description,
                Status = EditRequestStatus.Pending,
                ModerationStatus = ContentModerationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.RestaurantEditRequests.Add(editRequest);
            await _db.SaveChangesAsync(cancellationToken);

            _db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.EditRequest,
                ReferenceId = editRequest.RequestId,
                Status = TicketStatus.Open,
                Priority = 3,
                Description = $"Edycja dania \"{dish.DishName}\" (via UpdateDish)"
            });
        }

        if (request.IngredientIds is not null)
        {
            foreach (var old in dish.DishIngredients.ToList())
                _db.DishIngredients.Remove(old);

            var ingredients = request.IngredientIds.Count > 0
                ? await _db.Ingredients
                    .Where(i => request.IngredientIds.Contains(i.IngredientId))
                    .ToListAsync(cancellationToken)
                : new List<Ingredient>();

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
        }

        if (request.SectionIds is not null)
        {
            var existingAssignments = await _db.DishSectionAssignments
                .Where(dsa => dsa.DishId == dish.DishId)
                .ToListAsync(cancellationToken);

            foreach (var old in existingAssignments)
                _db.DishSectionAssignments.Remove(old);

            if (request.SectionIds.Count > 0)
            {
                var validSectionIds = await _db.MenuSections
                    .Where(ms => ms.RestaurantId == dish.Restaurant!.RestaurantId
                        && request.SectionIds.Contains(ms.SectionId))
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
            }
        }

        dish.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}

public class UpdateDishValidator : AbstractValidator<UpdateDishCommand>
{
    public UpdateDishValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Nieprawidłowe ID dania");

        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .MinimumLength(2).WithMessage("Nazwa dania musi mieć co najmniej 2 znaki")
                .MaximumLength(200).WithMessage("Nazwa dania może mieć maksymalnie 200 znaków");
        });
    }
}
