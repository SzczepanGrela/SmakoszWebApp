using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ChangeDishModerationStatus;

public record ChangeDishModerationStatusCommand(Guid PublicId, string NewStatus) : IRequest<ErrorOr<Success>>;

public class ChangeDishModerationStatusHandler : IRequestHandler<ChangeDishModerationStatusCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ChangeDishModerationStatusHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ChangeDishModerationStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        if (!Enum.TryParse<ContentModerationStatus>(request.NewStatus, true, out var newStatus))
            return DomainErrors.Dish.InvalidModerationStatus;

        var dish = await _db.Dishes
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId, cancellationToken);

        if (dish is null)
            return DomainErrors.Dish.NotFound;

        var oldStatus = dish.ModerationStatus;
        var oldValues = JsonSerializer.Serialize(new { ModerationStatus = oldStatus.ToString() });

        dish.ModerationStatus = newStatus;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "dishes",
            RecordId = dish.DishId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { ModerationStatus = newStatus.ToString() })
        });

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Dish,
            EntityId = dish.DishId,
            Actor = ModerationActor.Admin,
            Verdict = MapVerdict(newStatus),
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    private static ModerationVerdict MapVerdict(ContentModerationStatus status) => status switch
    {
        ContentModerationStatus.Approved => ModerationVerdict.Approved,
        ContentModerationStatus.Rejected => ModerationVerdict.Rejected,
        ContentModerationStatus.NeedsReview => ModerationVerdict.NeedsReview,
        _ => ModerationVerdict.NeedsReview
    };
}
