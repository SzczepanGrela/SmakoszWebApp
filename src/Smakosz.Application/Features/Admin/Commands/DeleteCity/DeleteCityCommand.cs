using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Admin.Commands.DeleteCity;

public record DeleteCityCommand(int CityId) : IRequest<ErrorOr<Success>>;

public class DeleteCityValidator : AbstractValidator<DeleteCityCommand>
{
    public DeleteCityValidator()
    {
        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("Nieprawidłowe ID miasta");
    }
}

public class DeleteCityHandler : IRequestHandler<DeleteCityCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteCityHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteCityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var city = await _db.Cities
            .FirstOrDefaultAsync(c => c.CityId == request.CityId, cancellationToken);

        if (city is null)
            return DomainErrors.City.NotFound;

        var hasRestaurants = await _db.Restaurants
            .AnyAsync(r => r.CityId == request.CityId, cancellationToken);

        if (hasRestaurants)
            return Error.Validation("CITY_HAS_RESTAURANTS", "Nie można usunąć miasta, które ma przypisane restauracje");

        _db.Cities.Remove(city);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
