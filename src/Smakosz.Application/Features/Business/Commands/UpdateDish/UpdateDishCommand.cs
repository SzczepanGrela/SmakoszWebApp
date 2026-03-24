using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.UpdateDish;

public record UpdateDishCommand(
    Guid PublicId,
    string? Name,
    decimal? Price,
    string? Description,
    int? Calories,
    bool? IsAvailable) : IRequest<ErrorOr<Success>>;

public class UpdateDishHandler : IRequestHandler<UpdateDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .Include(d => d.Restaurant)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.Restaurant?.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        if (request.Name is not null) dish.DishName = request.Name;
        if (request.Price.HasValue) dish.Price = request.Price.Value;
        if (request.Description is not null) dish.Description = request.Description;
        if (request.Calories.HasValue) dish.Calories = request.Calories.Value;
        if (request.IsAvailable.HasValue) dish.IsAvailable = request.IsAvailable.Value;

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
