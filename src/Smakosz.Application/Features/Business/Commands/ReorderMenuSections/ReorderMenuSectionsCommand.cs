using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.ReorderMenuSections;

public record ReorderMenuSectionsCommand(List<int> SectionIds) : IRequest<ErrorOr<Success>>;

public class ReorderMenuSectionsHandler : IRequestHandler<ReorderMenuSectionsCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReorderMenuSectionsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ReorderMenuSectionsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var sections = await _db.MenuSections
            .Where(ms => ms.RestaurantId == restaurant.RestaurantId)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < request.SectionIds.Count; i++)
        {
            var section = sections.FirstOrDefault(s => s.SectionId == request.SectionIds[i]);
            if (section is not null)
                section.DisplayOrder = i + 1;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
