using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetMenuSections;

public record GetMenuSectionsQuery() : IRequest<ErrorOr<List<BusinessMenuSectionDto>>>;

public class GetMenuSectionsHandler : IRequestHandler<GetMenuSectionsQuery, ErrorOr<List<BusinessMenuSectionDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMenuSectionsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<BusinessMenuSectionDto>>> Handle(GetMenuSectionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var sections = await _db.MenuSections
            .AsNoTracking()
            .Where(ms => ms.RestaurantId == restaurant.RestaurantId)
            .OrderBy(ms => ms.DisplayOrder)
            .Select(ms => new BusinessMenuSectionDto
            {
                MenuSectionId = ms.SectionId,
                Name = ms.SectionName,
                SortOrder = ms.DisplayOrder,
                DishCount = _db.DishSectionAssignments.Count(dsa => dsa.SectionId == ms.SectionId)
            })
            .ToListAsync(cancellationToken);

        return sections;
    }
}
