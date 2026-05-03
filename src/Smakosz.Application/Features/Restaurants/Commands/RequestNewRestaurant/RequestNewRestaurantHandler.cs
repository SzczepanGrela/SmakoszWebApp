using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestNewRestaurant;

public class RequestNewRestaurantHandler : IRequestHandler<RequestNewRestaurantCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public RequestNewRestaurantHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<int>> Handle(RequestNewRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        if (await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;
        if (request.Description is not null && await _forbiddenWords.ContainsAsync(request.Description, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        var hasPending = await _db.SystemTickets.AnyAsync(
            t => t.RequesterId == userId
                 && t.TicketType == TicketType.RestaurantRequest
                 && t.Status == TicketStatus.Open,
            cancellationToken);
        if (hasPending)
            return DomainErrors.Restaurant.RequestAlreadyPending;

        var payload = JsonSerializer.Serialize(new
        {
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            Description = request.Description,
            CityId = request.CityId,
            CuisineTypeId = request.CuisineTypeId
        });

        var now = DateTime.UtcNow;
        var ticket = new SystemTicket
        {
            TicketType = TicketType.RestaurantRequest,
            ReferenceId = 0,
            RequesterId = userId,
            Description = payload,
            Status = TicketStatus.Open,
            Priority = 3,
            CreatedAt = now
        };

        _db.SystemTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        return ticket.TicketId;
    }
}
