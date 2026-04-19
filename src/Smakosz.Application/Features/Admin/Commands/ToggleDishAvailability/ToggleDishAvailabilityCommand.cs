using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ToggleDishAvailability;

public record ToggleDishAvailabilityCommand(Guid PublicId, bool IsAvailable) : IRequest<ErrorOr<Success>>;

public class ToggleDishAvailabilityHandler : IRequestHandler<ToggleDishAvailabilityCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ToggleDishAvailabilityHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ToggleDishAvailabilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var dish = await _db.Dishes
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var oldValues = JsonSerializer.Serialize(new { dish.IsAvailable });
        dish.IsAvailable = request.IsAvailable;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "dishes",
            RecordId = dish.DishId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { dish.IsAvailable })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
