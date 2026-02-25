using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.UpdateDishAvailability;

public record UpdateDishAvailabilityCommand(Guid PublicId, bool IsAvailable) : IRequest<ErrorOr<Success>>;

public class UpdateDishAvailabilityHandler : IRequestHandler<UpdateDishAvailabilityCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateDishAvailabilityHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateDishAvailabilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var dish = await _db.Dishes
            .Include(d => d.Restaurant)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        if (dish.Restaurant.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.Business.NotOwner;

        dish.IsAvailable = request.IsAvailable;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
