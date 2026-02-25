using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.DeleteDish;

public record DeleteDishCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class DeleteDishHandler : IRequestHandler<DeleteDishCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteDishHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteDishCommand request, CancellationToken cancellationToken)
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

        _db.Dishes.Remove(dish);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
