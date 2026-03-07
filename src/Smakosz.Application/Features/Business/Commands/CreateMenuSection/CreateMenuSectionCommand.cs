using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.CreateMenuSection;

public record CreateMenuSectionCommand(string Name) : IRequest<ErrorOr<int>>;

public class CreateMenuSectionHandler : IRequestHandler<CreateMenuSectionCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public CreateMenuSectionHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<int>> Handle(CreateMenuSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var maxOrder = await _db.MenuSections
            .Where(ms => ms.RestaurantId == restaurant.RestaurantId)
            .Select(ms => (int?)ms.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var section = new MenuSection
        {
            RestaurantId = restaurant.RestaurantId,
            SectionName = request.Name,
            DisplayOrder = maxOrder + 1,
            ModerationStatus = ContentModerationStatus.Pending
        };

        _db.MenuSections.Add(section);
        await _db.SaveChangesAsync(cancellationToken);

        return section.SectionId;
    }
}
