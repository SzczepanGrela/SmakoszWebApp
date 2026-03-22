using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.DataCorrections.Commands.CreateDataCorrection;

public record CreateDataCorrectionCommand(
    string RestaurantSlug,
    string IssueType,
    string? Description,
    string? ProposedValue) : IRequest<ErrorOr<Success>>;

public class CreateDataCorrectionHandler : IRequestHandler<CreateDataCorrectionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublicConfigProvider _configProvider;

    public CreateDataCorrectionHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IPublicConfigProvider configProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _configProvider = configProvider;
    }

    public async Task<ErrorOr<Success>> Handle(CreateDataCorrectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Slug == request.RestaurantSlug, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (!Enum.TryParse<DataCorrectionIssueType>(request.IssueType, true, out var issueType))
            return Error.Validation("INVALID_ISSUE_TYPE", "Nieprawidłowy typ problemu");

        var deadlineDays = await _configProvider.GetIntAsync("datacorrection.response_deadline_days", 7, cancellationToken);

        var correction = new DataCorrectionRequest
        {
            RestaurantId = restaurant.RestaurantId,
            UserId = _currentUser.UserId.Value,
            IssueType = issueType,
            Description = request.Description,
            ProposedValue = request.ProposedValue != null ? JsonSerializer.Serialize(request.ProposedValue) : null,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            ResponseDeadline = DateTime.UtcNow.AddDays(deadlineDays)
        };

        _db.DataCorrectionRequests.Add(correction);
        await _db.SaveChangesAsync(cancellationToken);

        _db.SystemTickets.Add(new SystemTicket
        {
            TicketType = TicketType.DataCorrection,
            ReferenceId = correction.RequestId,
            Status = TicketStatus.Open,
            Priority = 3,
            Description = $"Korekta danych restauracji \"{restaurant.RestaurantName}\": {issueType}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
