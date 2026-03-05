using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.CreateEditRequest;

public record CreateEditRequestCommand(
    string ChangeType,
    string? Payload,
    string? NewName,
    string? NewDescription,
    string? NewAddress,
    string? NewPhone,
    string? NewWebsite) : IRequest<ErrorOr<Success>>;

public class CreateEditRequestHandler : IRequestHandler<CreateEditRequestCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateEditRequestHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(CreateEditRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Business.NotOwner;

        if (!Enum.TryParse<EditRequestChangeType>(request.ChangeType, true, out var changeType))
            changeType = EditRequestChangeType.General;

        var editRequest = new RestaurantEditRequest
        {
            RestaurantId = restaurant.RestaurantId,
            UserId = _currentUser.UserId.Value,
            ChangeType = changeType,
            Payload = request.Payload ?? "{}",
            NewName = request.NewName,
            NewDescription = request.NewDescription,
            NewAddress = request.NewAddress,
            NewPhone = request.NewPhone,
            NewWebsite = request.NewWebsite,
            Status = EditRequestStatus.Pending,
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
            Description = $"Wniosek o edycje restauracji \"{restaurant.RestaurantName}\": {changeType}"
        });

        var hasTextChanges = !string.IsNullOrEmpty(request.NewName) || !string.IsNullOrEmpty(request.NewDescription);
        if (hasTextChanges)
            editRequest.ModerationStatus = ContentModerationStatus.Pending;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
