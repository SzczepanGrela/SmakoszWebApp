using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.UpdateRestaurant;

public record UpdateRestaurantCommand(
    string? Name,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    int? CityId) : IRequest<ErrorOr<Success>>;

public class UpdateRestaurantHandler : IRequestHandler<UpdateRestaurantCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var hasTextChanges = request.Name is not null || request.Description is not null;

        // Non-text changes - apply immediately
        if (request.Address is not null) restaurant.Address = request.Address;
        if (request.Phone is not null) restaurant.Phone = request.Phone;
        if (request.Email is not null) restaurant.Email = request.Email;
        if (request.Website is not null) restaurant.Website = request.Website;
        if (request.CityId.HasValue) restaurant.CityId = request.CityId.Value;

        // Text changes - delegate to EditRequest flow (pessimistic: changes after approval)
        if (hasTextChanges)
        {
            var editRequest = new RestaurantEditRequest
            {
                RestaurantId = restaurant.RestaurantId,
                UserId = _currentUser.UserId.Value,
                ChangeType = EditRequestChangeType.InfoUpdate,
                Payload = "{}",
                NewName = request.Name,
                NewDescription = request.Description,
                Status = EditRequestStatus.Pending,
                ModerationStatus = ContentModerationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.RestaurantEditRequests.Add(editRequest);
            await _db.SaveChangesAsync(cancellationToken);

            _db.SystemTickets.Add(new SystemTicket
            {
                TicketType = TicketType.EditRequest,
                ReferenceId = editRequest.RequestId,
                Status = TicketStatus.Open,
                Priority = 3,
                Description = $"Edycja restauracji \"{restaurant.RestaurantName}\" (via UpdateRestaurant)"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
