using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestRestaurantClaim;

public class RequestRestaurantClaimHandler : IRequestHandler<RequestRestaurantClaimCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RequestRestaurantClaimHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<int>> Handle(RequestRestaurantClaimCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.PublicId == request.RestaurantPublicId, cancellationToken);
        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (restaurant.OwnerId != null)
            return DomainErrors.Restaurant.AlreadyClaimed;

        var alreadyOwns = await _db.Restaurants.AnyAsync(r => r.OwnerId == userId, cancellationToken);
        if (alreadyOwns)
            return DomainErrors.Business.UserAlreadyOwnsRestaurant;

        var hasPending = await _db.SystemTickets.AnyAsync(
            t => t.RequesterId == userId
                 && t.TicketType == TicketType.RestaurantClaim
                 && t.Status == TicketStatus.Open,
            cancellationToken);
        if (hasPending)
            return DomainErrors.Restaurant.ClaimAlreadyPending;

        var justification = request.Justification.Length > 2000
            ? request.Justification[..2000]
            : request.Justification;

        var now = DateTime.UtcNow;
        var ticket = new SystemTicket
        {
            TicketType = TicketType.RestaurantClaim,
            ReferenceId = restaurant.RestaurantId,
            RequesterId = userId,
            Description = justification,
            Status = TicketStatus.Open,
            Priority = 3,
            CreatedAt = now
        };

        _db.SystemTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        return ticket.TicketId;
    }
}
