using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetOpeningHours;

public record GetOpeningHoursQuery() : IRequest<ErrorOr<List<OpeningHoursDto>>>;

public class GetOpeningHoursHandler : IRequestHandler<GetOpeningHoursQuery, ErrorOr<List<OpeningHoursDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetOpeningHoursHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<OpeningHoursDto>>> Handle(GetOpeningHoursQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var hours = await _db.RestaurantOpeningHours
            .AsNoTracking()
            .Where(oh => oh.RestaurantId == restaurant.RestaurantId)
            .OrderBy(oh => oh.DayOfWeek)
            .Select(oh => new OpeningHoursDto
            {
                DayOfWeek = oh.DayOfWeek,
                OpenTime = oh.OpenTime,
                CloseTime = oh.CloseTime,
                IsClosed = oh.IsClosed
            })
            .ToListAsync(cancellationToken);

        return hours;
    }
}
