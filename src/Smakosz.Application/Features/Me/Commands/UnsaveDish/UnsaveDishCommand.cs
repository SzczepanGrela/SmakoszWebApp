using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.UnsaveDish;

public record UnsaveDishCommand(string DishSlug) : IRequest<ErrorOr<Success>>;

public class UnsaveDishHandler : IRequestHandler<UnsaveDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnsaveDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UnsaveDishCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .FirstOrDefaultAsync(d => d.Slug == request.DishSlug, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var saved = await _db.SavedDishes
            .FirstOrDefaultAsync(
                s => s.UserId == _currentUser.UserId.Value && s.DishId == dish.DishId,
                cancellationToken);

        if (saved is null)
            return DomainErrors.SavedDish.NotSaved;

        _db.SavedDishes.Remove(saved);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
