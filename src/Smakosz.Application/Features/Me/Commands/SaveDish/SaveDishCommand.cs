using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Me.Commands.SaveDish;

public record SaveDishCommand(string DishSlug) : IRequest<ErrorOr<Success>>;

public class SaveDishHandler : IRequestHandler<SaveDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SaveDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(SaveDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .FirstOrDefaultAsync(d => d.Slug == request.DishSlug, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var alreadySaved = await _db.SavedDishes.AnyAsync(
            s => s.UserId == _currentUser.UserId.Value && s.DishId == dish.DishId,
            cancellationToken);

        if (alreadySaved)
            return DomainErrors.SavedDish.AlreadySaved;

        _db.SavedDishes.Add(new SavedDish
        {
            UserId = _currentUser.UserId.Value,
            DishId = dish.DishId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
