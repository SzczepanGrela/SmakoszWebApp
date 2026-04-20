using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;

public record UpdateRestaurantAdminCommand(
    Guid PublicId,
    string? Name,
    string? Description,
    int? CuisineTypeId,
    int? PriceLevel,
    string? Address,
    string? PostalCode,
    string? Phone,
    string? Email,
    string? Website,
    int? CityId,
    int ExpectedVersion) : IRequest<ErrorOr<Success>>;

public class UpdateRestaurantAdminHandler : IRequestHandler<UpdateRestaurantAdminCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public UpdateRestaurantAdminHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateRestaurantAdminCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.PublicId == request.PublicId, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (restaurant.Version != request.ExpectedVersion)
            return DomainErrors.Restaurant.VersionMismatch;

        if (request.Name is not null && await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        if (request.Description is not null && await _forbiddenWords.ContainsAsync(request.Description, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        var oldSnapshot = new
        {
            restaurant.RestaurantName,
            restaurant.Description,
            restaurant.CuisineTypeId,
            restaurant.PriceLevel,
            restaurant.Address,
            restaurant.PostalCode,
            restaurant.Phone,
            restaurant.Email,
            restaurant.Website,
            restaurant.CityId
        };

        if (request.Name is not null) restaurant.RestaurantName = request.Name;
        if (request.Description is not null) restaurant.Description = request.Description;
        if (request.CuisineTypeId.HasValue) restaurant.CuisineTypeId = request.CuisineTypeId.Value;
        if (request.PriceLevel.HasValue) restaurant.PriceLevel = request.PriceLevel.Value;
        if (request.Address is not null) restaurant.Address = request.Address;
        if (request.PostalCode is not null) restaurant.PostalCode = request.PostalCode;
        if (request.Phone is not null) restaurant.Phone = request.Phone;
        if (request.Email is not null) restaurant.Email = request.Email;
        if (request.Website is not null) restaurant.Website = request.Website;
        if (request.CityId.HasValue) restaurant.CityId = request.CityId.Value;

        restaurant.Version++;

        var newSnapshot = new
        {
            restaurant.RestaurantName,
            restaurant.Description,
            restaurant.CuisineTypeId,
            restaurant.PriceLevel,
            restaurant.Address,
            restaurant.PostalCode,
            restaurant.Phone,
            restaurant.Email,
            restaurant.Website,
            restaurant.CityId
        };

        _db.AuditLogs.Add(AuditLogHelper.BuildEntry(
            "restaurants",
            restaurant.RestaurantId,
            AuditOperation.Update,
            _currentUser.UserId?.ToString(),
            oldSnapshot,
            newSnapshot));

        if (restaurant.OwnerId.HasValue)
        {
            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == restaurant.OwnerId.Value, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = restaurant.OwnerId.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Title = "Dane restauracji zaktualizowane",
                Message = $"Administrator zaktualizował dane restauracji \"{restaurant.RestaurantName}\".",
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
