using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateIngredient;

public record CreateIngredientCommand(
    string Name,
    bool IsAllergen,
    bool IsVegetarian,
    bool IsVegan,
    bool IsGlutenFree,
    bool IsLactoseFree) : IRequest<ErrorOr<int>>;

public class CreateIngredientValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa składnika jest wymagana")
            .MaximumLength(100).WithMessage("Nazwa składnika może mieć maksymalnie 100 znaków");
    }
}

public class CreateIngredientHandler : IRequestHandler<CreateIngredientCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateIngredientHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<int>> Handle(CreateIngredientCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.Ingredients
            .AnyAsync(i => i.IngredientName.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.Ingredient.AlreadyExists;

        var ingredient = new Ingredient
        {
            IngredientName = request.Name,
            IsAllergen = request.IsAllergen,
            IsVegetarian = request.IsVegetarian,
            IsVegan = request.IsVegan,
            IsGlutenFree = request.IsGlutenFree,
            IsLactoseFree = request.IsLactoseFree,
            CreatedAt = _dateTime.UtcNow
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Ingredients",
            RecordId = ingredient.IngredientId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            NewValues = JsonSerializer.Serialize(new { request.Name, request.IsAllergen, request.IsVegetarian, request.IsVegan, request.IsGlutenFree, request.IsLactoseFree })
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ingredient.IngredientId;
    }
}
